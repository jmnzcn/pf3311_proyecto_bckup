/**
 * PF-3311 — recibe CSV de sesiones desde el .exe y los guarda en Google Drive.
 *
 * DESPLIEGUE (una vez):
 * 1. https://script.google.com → Nuevo proyecto → pegar este archivo.
 * 2. Proyecto → Configuración → Propiedades del script → Añadir:
 *      UPLOAD_SECRET = (el mismo valor que csvDriveUploadSecret en Unity)
 * 3. Implementar → Nueva implementación → Aplicación web
 *      Ejecutar como: Yo
 *      Quién tiene acceso: Cualquier persona (incluso anónimos)  ← obligatorio para el .exe
 * 4. Copiar la URL que termina en /exec → csvDriveUploadUrl en ExperimentLogic (SampleScene).
 *
 * Carpeta destino: CSVs/{ParticipantCode}_{SessionID}/ dentro del folder ID de abajo.
 */
var DRIVE_PARENT_FOLDER_ID = '1T1K1zRq9L2PHaSYkhDDBPY5b1BHKKp1Y';
var CSV_ROOT_FOLDER_NAME = 'CSVs';
var UPLOAD_SECRET = '__UPLOAD_SECRET__';
var UPLOAD_SECRET_UNSET = '__UPLOAD_SECRET__';

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
