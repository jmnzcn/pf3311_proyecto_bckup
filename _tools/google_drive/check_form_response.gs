/**
 * PF-3311 — verifica si un participante ya envió un Google Form (por código P##).
 * Desplegar en el MISMO proyecto Apps Script que upload_session_csv.gs (misma URL /exec).
 *
 * GET ?secret=...&form=profile|A|B|C&participant=P03&sinceMs=1710000000000
 * → { "ok": true, "submitted": true|false }
 */
var FORM_IDS = {
  profile: '1XjG9GBr71tyhfF2sFWn7RcKfHkBAjCnufZJKZXoP6zA',
  A: '1eXYTw-5RbUbLtk1trghoP2XejZCsHZs6Fyk8uuhUeak',
  B: '1MLqIh__LvA7Da5-uJt6izsD-pAyALmO9Je7GlrZpTEg',
  C: '1mQhV7SZF4X0DRYw-pOhG62GHJxHK6d0jNTCn8pV_ceQ'
};

function doGet(e) {
  try {
    if (!UPLOAD_SECRET || UPLOAD_SECRET === '__UPLOAD_SECRET__') {
      return jsonResponse_({ ok: false, error: 'UPLOAD_SECRET not configured.' });
    }

    var params = (e && e.parameter) ? e.parameter : {};
    if (params.secret !== UPLOAD_SECRET) {
      return jsonResponse_({ ok: false, error: 'Unauthorized.' });
    }

    var formKey = String(params.form || '').trim();
    var participant = normalizeParticipantCode_(params.participant);
    var sinceMs = Number(params.sinceMs || 0);

    if (!formKey || !participant) {
      return jsonResponse_({ ok: false, error: 'Missing form or participant.' });
    }

    var formId = FORM_IDS[formKey];
    if (!formId) {
      return jsonResponse_({ ok: false, error: 'Unknown form key: ' + formKey });
    }

    var submitted = hasParticipantResponseSince_(formId, participant, sinceMs);
    return jsonResponse_({ ok: true, submitted: submitted });
  } catch (err) {
    return jsonResponse_({ ok: false, error: String(err) });
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

function jsonResponse_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
