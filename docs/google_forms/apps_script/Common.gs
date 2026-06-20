/**
 * PF-3311 — Helpers compartidos para crear formularios con FormApp.
 * Incluí este archivo en el mismo proyecto que Form0…Form3.
 */

var PF3311_SCALE_LABEL_LOW = 'Totalmente en desacuerdo';
var PF3311_SCALE_LABEL_HIGH = 'Totalmente de acuerdo';

var PF3311_MECUE_MODULE_I = [
  'El producto es fácil de usar.',
  'Las funciones del asistente apoyan lo que necesitaba hacer en los casos de este bloque.',
  'Es evidente rápidamente cómo usar el producto.',
  'Considero que el producto es extremadamente útil.',
  'Los procedimientos de uso del producto son sencillos de entender.',
  'Con la ayuda de este asistente pude avanzar en los casos de este bloque.'
];

var PF3311_MECUE_MODULE_II = [
  'El producto está diseñado de forma creativa.',
  'El diseño se ve atractivo.',
  'El producto es elegante / tiene estilo.',
  'El personaje del agente me resultó cercano.',
  'Me costaría completar tareas similares sin un agente como este.'
];

var PF3311_MECUE_MODULE_III = [
  'El producto me entusiasma.',
  'El producto me cansa.',
  'El producto me molesta.',
  'El producto me relaja.',
  'Al usar este producto me siento agotado/a.',
  'El producto me hace sentir feliz.',
  'El producto me frustra.',
  'El producto me hace sentir eufórico/a.',
  'El producto me hace sentir pasivo/a.',
  'El producto me calma.',
  'Al usar este producto me siento alegre.',
  'El producto me enoja.'
];

var PF3311_MECUE_MODULE_IV = [
  'Volvería a usar un asistente como este para tareas similares.',
  'Al usar el producto, pierdo la noción del tiempo.'
];

var PF3311_RAW_TLX = [
  'La tarea me exigió mucha actividad mental y concentración.',
  'Sentí presión de tiempo mientras resolvía los casos.',
  'Tuve que trabajar muy duro (esfuerzo) para completar la tarea.',
  'Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.',
  'Me sentí seguro/a de cómo desempeñé la tarea en este bloque.',
  'La tarea me resultó exigente en general.'
];

var PF3311_MODULE_V_OPTIONS = [
  '-5 — Muy malo',
  '-4',
  '-3',
  '-2',
  '-1',
  '0 — Neutral',
  '+1',
  '+2',
  '+3',
  '+4',
  '+5 — Muy bueno'
];

var PF3311_AGE_RANGES = [
  '18–24 años',
  '25–34 años',
  '35–44 años',
  '45–54 años',
  '55–64 años',
  '65 años o más'
];

/** Anonimato: sin recopilar correo. */
function configurePF3311Form_(form) {
  form.setCollectEmail(false);
  return form;
}

function addParticipantCode_(form) {
  return form.addTextItem()
    .setTitle('Código de participante')
    .setHelpText(
      'Este campo se completa automáticamente desde Unity. Si está vacío, usá el mismo código que ingresaste en la aplicación (formato P01, P02, …).'
    )
    .setValidation(
      FormApp.createTextValidation()
        .requireTextMatchesPattern('^P\\d{2}$')
        .setHelpText('Usá el formato P01, P02, … igual que en Unity.')
        .build()
    )
    .setRequired(true);
}

function addAgeRangeItem_(form) {
  return addMultipleChoice_(form, '¿En qué rango de edad te encontrás?', PF3311_AGE_RANGES);
}

function addMultipleChoice_(form, title, options) {
  var item = form.addMultipleChoiceItem()
    .setTitle(title)
    .setRequired(true);
  var choices = options.map(function (label) {
    return item.createChoice(label);
  });
  item.setChoices(choices);
  return item;
}

function addScale17_(form, title) {
  return form.addScaleItem()
    .setTitle(title)
    .setBounds(1, 7)
    .setLabels(PF3311_SCALE_LABEL_LOW, PF3311_SCALE_LABEL_HIGH)
    .setRequired(true);
}

function addScaleItems_(form, titles) {
  titles.forEach(function (title) {
    addScale17_(form, title);
  });
}

function addSection_(form, title) {
  return form.addSectionHeaderItem().setTitle(title);
}

function addModuleV_(form, title) {
  return addMultipleChoice_(form, title, PF3311_MODULE_V_OPTIONS);
}

function addMeCueModulesI_III_IV_(form) {
  addSection_(form, 'meCUE — Módulo I (cualidades instrumentales)');
  addScaleItems_(form, PF3311_MECUE_MODULE_I);

  addSection_(form, 'meCUE — Módulo III (emociones)');
  addScaleItems_(form, PF3311_MECUE_MODULE_III);

  addSection_(form, 'meCUE — Módulo IV (consecuencias de uso — 2 ítems curados)');
  addScaleItems_(form, PF3311_MECUE_MODULE_IV);
}

function addMeCueModuleII_(form) {
  addSection_(form, 'meCUE — Módulo II (cualidades no instrumentales — avatar)');
  addScaleItems_(form, PF3311_MECUE_MODULE_II);
}

function addRawTlxSection_(form) {
  addSection_(form, 'Carga cognitiva (RAW-TLX adaptado)');
  addScaleItems_(form, PF3311_RAW_TLX);
}

/** Escribe URLs en Registro de ejecución (Ver → Registros). */
function logFormUrls_(form, label) {
  Logger.log('=== ' + label + ' ===');
  Logger.log('Editar:    ' + form.getEditUrl());
  Logger.log('Responder: ' + form.getPublishedUrl());
}
