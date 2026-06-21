/**
 * PF-3311 — Eliminar respuestas de participantes SOLO en Google Forms (vivo).
 * El backup en Drive NO se modifica.
 *
 * Editor Apps Script:
 *   DELETE_FORM_RESPONSES_BY_PARTICIPANTS('P01')
 *   DELETE_FORM_RESPONSES_BY_PARTICIPANTS('P01,P02')
 *
 * Web app (requiere confirm=1):
 *   GET .../exec?secret=...&action=deleteFormResponses&participants=P01,P02&confirm=1
 */

/**
 * @param {string|string[]} participantCodesRaw ej. "P01" o "P01,P02" o ["P01","P02"]
 * @return {Object} resumen por formulario
 */
function DELETE_FORM_RESPONSES_BY_PARTICIPANTS(participantCodesRaw) {
  var codes = parseParticipantCodesParam_(participantCodesRaw);
  if (!codes.length) {
    throw new Error('Indicá al menos un código (ej. P01 o P01,P02).');
  }

  FormApp.openById(FORM_IDS.profile);

  var summary = {
    participants: codes,
    backupUntouched: true,
    forms: {}
  };
  var keys = ['profile', 'A', 'B', 'C'];

  for (var i = 0; i < keys.length; i++) {
    var key = keys[i];
    var formId = FORM_IDS[key];
    if (!formId) {
      summary.forms[key] = { ok: false, error: 'Missing form id' };
      continue;
    }

    try {
      var deletedFromForm = deleteResponsesForParticipantsInForm_(formId, codes);
      summary.forms[key] = {
        ok: true,
        formId: formId,
        deletedFromForm: deletedFromForm
      };
    } catch (err) {
      summary.forms[key] = {
        ok: false,
        formId: formId,
        error: String(err)
      };
    }
  }

  Logger.log(JSON.stringify(summary, null, 2));
  return summary;
}

function handleDeleteFormResponsesGet_(params) {
  if (String(params.confirm || '') !== '1') {
    return jsonResponse({
      ok: false,
      error: 'Acción destructiva: agregá confirm=1 (ej. participants=P01&confirm=1).'
    });
  }

  var participants = params.participants || params.participant || '';
  if (!String(participants).trim()) {
    return jsonResponse({
      ok: false,
      error: 'Falta participants (ej. P01 o P01,P02).'
    });
  }

  var summary = DELETE_FORM_RESPONSES_BY_PARTICIPANTS(participants);
  return jsonResponse({
    ok: true,
    action: 'deleteFormResponses',
    summary: summary
  });
}

function parseParticipantCodesParam_(raw) {
  var parts = Array.isArray(raw) ? raw : String(raw || '').split(',');
  var seen = {};
  var out = [];

  for (var i = 0; i < parts.length; i++) {
    var code = normalizeParticipantCode_(String(parts[i] || '').trim());
    if (!code || seen[code]) {
      continue;
    }
    seen[code] = true;
    out.push(code);
  }

  return out;
}

function deleteResponsesForParticipantsInForm_(formId, participantCodes) {
  var form = FormApp.openById(formId);
  var codesSet = buildParticipantCodesSet_(participantCodes);
  var responses = form.getResponses();
  var deleted = 0;

  for (var r = responses.length - 1; r >= 0; r--) {
    var response = responses[r];
    var code = extractParticipantCodeFromResponse_(response);
    if (!code || !codesSet[code]) {
      continue;
    }
    form.deleteResponse(response.getId());
    deleted++;
  }

  return deleted;
}

function extractParticipantCodeFromResponse_(response) {
  var itemResponses = response.getItemResponses();
  for (var j = 0; j < itemResponses.length; j++) {
    var item = itemResponses[j].getItem();
    var title = String(item.getTitle() || '').toLowerCase();
    if (title.indexOf('código de participante') === -1 && title.indexOf('codigo de participante') === -1) {
      continue;
    }
    return normalizeParticipantCode_(String(itemResponses[j].getResponse() || ''));
  }
  return '';
}

function buildParticipantCodesSet_(participantCodes) {
  var set = {};
  for (var i = 0; i < participantCodes.length; i++) {
    set[normalizeParticipantCode_(participantCodes[i])] = true;
  }
  return set;
}
