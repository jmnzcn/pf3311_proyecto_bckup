/**
 * PF-3311 â€” recibe CSV de sesiones desde el .exe y los guarda en Google Drive.
 *
 * DESPLIEGUE (una vez):
 * 1. https://script.google.com â†’ Nuevo proyecto â†’ pegar este archivo.
 * 2. Proyecto â†’ ConfiguraciÃ³n â†’ Propiedades del script â†’ AÃ±adir:
 *      UPLOAD_SECRET = (el mismo valor que csvDriveUploadSecret en Unity)
 * 3. Implementar â†’ Nueva implementaciÃ³n â†’ AplicaciÃ³n web
 *      Ejecutar como: Yo
 *      QuiÃ©n tiene acceso: Cualquier persona
 * 4. Copiar la URL que termina en /exec â†’ csvDriveUploadUrl en ExperimentLogic (SampleScene).
 *
 * Carpeta destino: CSVs/{ParticipantCode}_{SessionID}/ dentro del folder ID de abajo.
 */
var DRIVE_PARENT_FOLDER_ID = '1T1K1zRq9L2PHaSYkhDDBPY5b1BHKKp1Y';
var CSV_ROOT_FOLDER_NAME = 'CSVs';
var UPLOAD_SECRET = '784c915b3a664e08a32ce9a8572937f2';
var UPLOAD_SECRET_UNSET = '__UPLOAD_SECRET__';

/** Una sola vez: en el editor, elegí esta función en el menú desplegable y pulsá Ejecutar ▶. */
function AUTORIZAR_PERMISOS_UNA_VEZ() {
  DriveApp.getFolderById(DRIVE_PARENT_FOLDER_ID);
  FormApp.openById(FORM_IDS.profile);
  return 'Listo: permisos de Drive y Forms aprobados.';
}

function doPost(e) {
  try {
    if (!UPLOAD_SECRET || UPLOAD_SECRET === UPLOAD_SECRET_UNSET) {
      return jsonResponse({ ok: false, error: 'UPLOAD_SECRET not configured.' });
    }

    if (!e || !e.postData || !e.postData.contents) {
      return jsonResponse({ ok: false, error: 'Missing POST body.' });
    }

    var payload = JSON.parse(e.postData.contents);
    if (!payload || payload.secret !== UPLOAD_SECRET) {
      return jsonResponse({ ok: false, error: 'Unauthorized.' });
    }

    if (!payload.sessionFolderName || !payload.files || !payload.files.length) {
      return jsonResponse({ ok: false, error: 'Missing sessionFolderName or files.' });
    }

    var csvRoot = getOrCreateSubfolder_(DRIVE_PARENT_FOLDER_ID, CSV_ROOT_FOLDER_NAME);
    var sessionFolder = getOrCreateSubfolder_(csvRoot.getId(), sanitizeFolderName_(payload.sessionFolderName));
    var saved = 0;

    for (var i = 0; i < payload.files.length; i++) {
      var entry = payload.files[i];
      if (!entry || !entry.name || !entry.contentBase64) {
        continue;
      }

      var fileName = sanitizeFileName_(entry.name);
      if (!fileName.toLowerCase().endsWith('.csv')) {
        continue;
      }

      var bytes = Utilities.base64Decode(entry.contentBase64);
      var blob = Utilities.newBlob(bytes, 'text/csv; charset=utf-8', fileName);
      upsertFile_(sessionFolder, fileName, blob);
      saved++;
    }

    if (saved === 0) {
      return jsonResponse({ ok: false, error: 'No CSV files saved.' });
    }

    return jsonResponse({ ok: true, fileCount: saved, sessionFolderName: payload.sessionFolderName });
  } catch (err) {
    return jsonResponse({ ok: false, error: String(err) });
  }
}

function getOrCreateSubfolder_(parentId, name) {
  var parent = DriveApp.getFolderById(parentId);
  var matches = parent.getFoldersByName(name);
  if (matches.hasNext()) {
    return matches.next();
  }
  return parent.createFolder(name);
}

function upsertFile_(folder, fileName, blob) {
  var existing = folder.getFilesByName(fileName);
  while (existing.hasNext()) {
    existing.next().setTrashed(true);
  }
  folder.createFile(blob);
}

function sanitizeFolderName_(name) {
  return String(name).replace(/[\\/:*?"<>|]/g, '_').substring(0, 120);
}

function sanitizeFileName_(name) {
  var base = String(name).replace(/[\\/:*?"<>|]/g, '_');
  var parts = base.split('.');
  if (parts.length < 2) {
    return base.substring(0, 120);
  }
  var ext = parts.pop();
  return parts.join('.').substring(0, 100) + '.' + ext.substring(0, 10);
}

function jsonResponse(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}

var FORM_IDS = {
  profile: '1XjG9GBr71tyhfF2sFWn7RcKfHkBAjCnufZJKZXoP6zA',
  A: '1eXYTw-5RbUbLtk1trghoP2XejZCsHZs6Fyk8uuhUeak',
  B: '1MLqIh__LvA7Da5-uJt6izsD-pAyALmO9Je7GlrZpTEg',
  C: '1mQhV7SZF4X0DRYw-pOhG62GHJxHK6d0jNTCn8pV_ceQ'
};

function doGet(e) {
  try {
    var params = (e && e.parameter) ? e.parameter : {};
    if (String(params.ping || '') === '1') {
      return jsonResponse({ ok: true, ping: true });
    }

    if (!UPLOAD_SECRET || UPLOAD_SECRET === UPLOAD_SECRET_UNSET) {
      return jsonResponse({ ok: false, error: 'UPLOAD_SECRET not configured.' });
    }

    if (params.secret !== UPLOAD_SECRET) {
      return jsonResponse({ ok: false, error: 'Unauthorized.' });
    }

    var formKey = String(params.form || '').trim();
    var participant = normalizeParticipantCode_(params.participant);
    var sinceMs = Number(params.sinceMs || 0);

    if (!formKey || !participant) {
      return jsonResponse({ ok: false, error: 'Missing form or participant.' });
    }

    var formId = FORM_IDS[formKey];
    if (!formId) {
      return jsonResponse({ ok: false, error: 'Unknown form key: ' + formKey });
    }

    var submitted = hasParticipantResponseSince_(formId, participant, sinceMs);
    return jsonResponse({ ok: true, submitted: submitted });
  } catch (err) {
    return jsonResponse({ ok: false, error: String(err) });
  }
}

function hasParticipantResponseSince_(formId, participantCode, sinceMs) {
  var form = FormApp.openById(formId);
  var responses = form.getResponses();
  var sinceDate = sinceMs > 0 ? new Date(sinceMs - 60000) : null;

  for (var i = responses.length - 1; i >= 0; i--) {
    var response = responses[i];
    if (sinceDate && response.getTimestamp() < sinceDate) {
      continue;
    }

    var itemResponses = response.getItemResponses();
    for (var j = 0; j < itemResponses.length; j++) {
      var item = itemResponses[j].getItem();
      var title = String(item.getTitle() || '').toLowerCase();
      if (title.indexOf('código de participante') === -1 && title.indexOf('codigo de participante') === -1) {
        continue;
      }

      var value = normalizeParticipantCode_(String(itemResponses[j].getResponse() || ''));
      if (value === participantCode) {
        return true;
      }
    }
  }

  return false;
}

function normalizeParticipantCode_(raw) {
  var value = String(raw || '').trim().toUpperCase();
  if (!value) {
    return '';
  }

  if (value.charAt(0) !== 'P') {
    return value;
  }

  var digits = value.substring(1).replace(/\D/g, '');
  if (!digits) {
    return value;
  }

  var number = parseInt(digits, 10);
  if (isNaN(number) || number < 1) {
    return value;
  }

  return number < 10 ? 'P0' + number : 'P' + number;
}

function authorizeSelf_() {
  DriveApp.getFolderById(DRIVE_PARENT_FOLDER_ID);
  FormApp.openById(FORM_IDS.profile);
  return 'authorized';
}
