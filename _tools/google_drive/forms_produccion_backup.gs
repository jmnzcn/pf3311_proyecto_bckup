/**
 * PF-3311 — Backup incremental de encuestas Google Forms → carpeta Drive de producción.
 *
 * Fuente: respuestas EN VIVO en Google Forms (FormApp), no archivos locales.
 * Destino: CSV en Drive (un archivo por formulario).
 * Regla: el backup SIEMPRE gana. Si P01 (u otro código) ya está en backup,
 *        se ignora la fila de Forms — nunca se sobrescribe ni se reemplaza nada.
 *
 * Ejecutar en Apps Script (editor):
 *   BACKUP_FORMS_PRODUCCION_A_DRIVE
 *
 * O vía web app (mismo secreto que CSV upload):
 *   GET .../exec?secret=...&action=backupFormsProduccion
 */

var FORMS_BACKUP_FOLDER_ID = '1RLLESYhJbJkBOnNdV6uZTaehl-t-W-Cp';

/** formKey → nombre del CSV en la carpeta de backup */
var FORM_BACKUP_FILES = {
  profile: 'Form0_Perfil.csv',
  A: 'PostBloqueA.csv',
  B: 'PostBloqueB.csv',
  C: 'PostBloqueC.csv'
};

/**
 * Copia/actualiza los 4 CSV de backup leyendo Forms en Google.
 * @return {Object} resumen por archivo
 */
function BACKUP_FORMS_PRODUCCION_A_DRIVE() {
  DriveApp.getFolderById(FORMS_BACKUP_FOLDER_ID);

  var summary = {};
  var keys = ['profile', 'A', 'B', 'C'];

  for (var i = 0; i < keys.length; i++) {
    var key = keys[i];
    var formId = FORM_IDS[key];
    var fileName = FORM_BACKUP_FILES[key];
    if (!formId || !fileName) {
      summary[key] = { ok: false, error: 'Missing form id or filename' };
      continue;
    }

    try {
      var liveCsv = exportFormToCsv_(formId);
      var merged = mergeCsvIncrementalInDrive_(FORMS_BACKUP_FOLDER_ID, fileName, liveCsv);
      summary[key] = {
        ok: true,
        fileName: fileName,
        added: merged.added,
        skipped: merged.skipped,
        total: merged.total,
        unchanged: merged.unchanged
      };
      if (merged.warning) {
        summary[key].warning = merged.warning;
      }
    } catch (err) {
      summary[key] = { ok: false, fileName: fileName, error: String(err) };
    }
  }

  var stamp = Utilities.formatDate(new Date(), 'America/Costa_Rica', "yyyy-MM-dd'T'HH:mm:ssXXX");
  writeTextFileInFolder_(FORMS_BACKUP_FOLDER_ID, 'last_backup_utc.txt', stamp + '\n');

  Logger.log(JSON.stringify(summary, null, 2));
  return summary;
}

function handleBackupFormsProduccionGet_() {
  var summary = BACKUP_FORMS_PRODUCCION_A_DRIVE();
  return jsonResponse({ ok: true, action: 'backupFormsProduccion', summary: summary });
}

// --- Export Form → CSV (compatible con export de Google Forms) ---

function exportFormToCsv_(formId) {
  var form = FormApp.openById(formId);
  var items = getResponseableItems_(form);
  var headers = ['Marca temporal'];
  for (var h = 0; h < items.length; h++) {
    headers.push(String(items[h].getTitle() || ''));
  }

  var responses = form.getResponses();
  var rows = [headers];

  for (var r = 0; r < responses.length; r++) {
    rows.push(buildResponseRow_(responses[r], items, headers));
  }

  return rowsToCsv_(rows);
}

function getResponseableItems_(form) {
  var skip = {
    PAGE_BREAK: true,
    SECTION_HEADER: true,
    IMAGE: true,
    VIDEO: true
  };
  var all = form.getItems();
  var out = [];
  for (var i = 0; i < all.length; i++) {
    var type = String(all[i].getType());
    if (skip[type]) {
      continue;
    }
    out.push(all[i]);
  }
  return out;
}

function buildResponseRow_(response, items, headers) {
  var byId = {};
  var itemResponses = response.getItemResponses();
  for (var j = 0; j < itemResponses.length; j++) {
    byId[itemResponses[j].getItem().getId()] = itemResponses[j].getResponse();
  }

  var row = [formatTimestampForCsv_(response.getTimestamp())];
  for (var k = 0; k < items.length; k++) {
    var val = byId[items[k].getId()];
    row.push(formatResponseValue_(val));
  }
  return row;
}

function formatResponseValue_(val) {
  if (val === undefined || val === null) {
    return '';
  }
  if (Array.isArray(val)) {
    return val.join(', ');
  }
  return String(val);
}

function formatTimestampForCsv_(date) {
  return Utilities.formatDate(date, Session.getScriptTimeZone() || 'America/Costa_Rica', 'yyyy-MM-dd HH:mm:ss');
}

// --- Merge incremental en Drive ---

function mergeCsvIncrementalInDrive_(folderId, fileName, liveCsvString) {
  var live = parseCsv_(liveCsvString);
  if (!live.rows.length) {
    return { added: 0, skipped: 0, total: 0, unchanged: true };
  }

  var existingFile = findFileInFolder_(folderId, fileName);
  var added = 0;
  var skipped = 0;

  if (!existingFile) {
    added = Math.max(0, live.rows.length - 1);
    createBackupFile_(folderId, fileName, liveCsvString);
    return { added: added, skipped: 0, total: added, unchanged: false };
  }

  var backup = parseCsv_(existingFile.getBlob().getDataAsString('UTF-8'));
  if (!backup.rows.length) {
    added = Math.max(0, live.rows.length - 1);
    updateBackupFile_(existingFile, liveCsvString);
    return { added: added, skipped: 0, total: added, unchanged: false };
  }

  var headers = backup.rows[0];
  if (headers.join('|') !== live.rows[0].join('|')) {
    return {
      added: 0,
      skipped: Math.max(0, live.rows.length - 1),
      total: backup.rows.length - 1,
      unchanged: true,
      warning: 'Encabezados distintos; backup intacto.'
    };
  }

  var participantCol = findParticipantColumnIndex_(headers);
  var seenCodes = {};
  var seenFingerprints = {};
  var mergedRows = [headers];

  for (var b = 1; b < backup.rows.length; b++) {
    var brow = backup.rows[b];
    mergedRows.push(brow);
    rememberRowIdentity_(brow, participantCol, seenCodes, seenFingerprints);
  }

  for (var l = 1; l < live.rows.length; l++) {
    var lrow = live.rows[l];
    if (isRowAlreadyBackedUp_(lrow, participantCol, seenCodes, seenFingerprints)) {
      skipped++;
      continue;
    }
    rememberRowIdentity_(lrow, participantCol, seenCodes, seenFingerprints);
    mergedRows.push(lrow);
    added++;
  }

  if (added === 0) {
    return {
      added: 0,
      skipped: skipped,
      total: mergedRows.length - 1,
      unchanged: true
    };
  }

  updateBackupFile_(existingFile, rowsToCsv_(mergedRows));
  return { added: added, skipped: skipped, total: mergedRows.length - 1, unchanged: false };
}

function findParticipantColumnIndex_(headers) {
  for (var i = 0; i < headers.length; i++) {
    var h = String(headers[i] || '').toLowerCase();
    if (h.indexOf('código de participante') !== -1 || h.indexOf('codigo de participante') !== -1) {
      return i;
    }
  }
  return -1;
}

function participantCodeFromRow_(row, participantCol) {
  if (participantCol < 0 || participantCol >= row.length) {
    return '';
  }
  return normalizeParticipantCode_(String(row[participantCol] || ''));
}

function rowFingerprint_(row) {
  return row.join('\t');
}

/** Marca código de participante o huella de fila ya presente en backup. */
function rememberRowIdentity_(row, participantCol, seenCodes, seenFingerprints) {
  var code = participantCodeFromRow_(row, participantCol);
  if (code) {
    seenCodes[code] = true;
    return;
  }
  seenFingerprints[rowFingerprint_(row)] = true;
}

/** true si el backup ya tiene este participante (p. ej. P01) o la misma fila exacta. */
function isRowAlreadyBackedUp_(row, participantCol, seenCodes, seenFingerprints) {
  var code = participantCodeFromRow_(row, participantCol);
  if (code && seenCodes[code]) {
    return true;
  }
  return seenFingerprints[rowFingerprint_(row)] === true;
}

function findFileInFolder_(folderId, fileName) {
  var folder = DriveApp.getFolderById(folderId);
  var it = folder.getFilesByName(fileName);
  return it.hasNext() ? it.next() : null;
}

function createBackupFile_(folderId, fileName, content) {
  var folder = DriveApp.getFolderById(folderId);
  var blob = Utilities.newBlob(content, 'text/csv; charset=utf-8', fileName);
  folder.createFile(blob);
}

function updateBackupFile_(file, content) {
  file.setContent(content);
}

function writeTextFileInFolder_(folderId, fileName, content) {
  var existing = findFileInFolder_(folderId, fileName);
  if (existing) {
    updateBackupFile_(existing, content);
    return;
  }
  createBackupFile_(folderId, fileName, content);
}

// --- CSV parse / serialize ---

function rowsToCsv_(rows) {
  var lines = [];
  for (var i = 0; i < rows.length; i++) {
    var cells = [];
    for (var j = 0; j < rows[i].length; j++) {
      cells.push(csvEscape_(rows[i][j]));
    }
    lines.push(cells.join(','));
  }
  return lines.join('\n') + '\n';
}

function csvEscape_(value) {
  var s = value === undefined || value === null ? '' : String(value);
  if (s.indexOf('"') !== -1 || s.indexOf(',') !== -1 || s.indexOf('\n') !== -1 || s.indexOf('\r') !== -1) {
    return '"' + s.replace(/"/g, '""') + '"';
  }
  return s;
}

function parseCsv_(text) {
  var rows = [];
  var row = [];
  var cell = '';
  var inQuotes = false;

  for (var i = 0; i < text.length; i++) {
    var c = text.charAt(i);
    if (inQuotes) {
      if (c === '"') {
        if (i + 1 < text.length && text.charAt(i + 1) === '"') {
          cell += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        cell += c;
      }
      continue;
    }

    if (c === '"') {
      inQuotes = true;
    } else if (c === ',') {
      row.push(cell);
      cell = '';
    } else if (c === '\n') {
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else if (c === '\r') {
      // ignore
    } else {
      cell += c;
    }
  }

  if (cell.length > 0 || row.length > 0) {
    row.push(cell);
    rows.push(row);
  }

  return { rows: rows };
}
