/**
 * PF-3311 — Formulario 3: Post bloque C (agente virtual)
 * Ejecutar → createPF3311_Form3_PostC
 * Este archivo es autocontenido (no requiere Common.gs).
 */
if (typeof configurePF3311Form_ === 'undefined') {
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
    '-5 — Muy malo', '-4', '-3', '-2', '-1', '0 — Neutral',
    '+1', '+2', '+3', '+4', '+5 — Muy bueno'
  ];

  configurePF3311Form_ = function (form) {
    form.setCollectEmail(false);
    return form;
  };

  addParticipantCode_ = function (form) {
    return form.addTextItem()
      .setTitle('Código de participante')
      .setHelpText('Usá el mismo código que te indicó el investigador (el mismo que ingresaste en Unity).')
      .setRequired(true);
  };

  addMultipleChoice_ = function (form, title, options) {
    var item = form.addMultipleChoiceItem()
      .setTitle(title)
      .setRequired(true);
    var choices = options.map(function (label) {
      return item.createChoice(label);
    });
    item.setChoices(choices);
    return item;
  };

  addScale17_ = function (form, title) {
    return form.addScaleItem()
      .setTitle(title)
      .setBounds(1, 7)
      .setLabels(PF3311_SCALE_LABEL_LOW, PF3311_SCALE_LABEL_HIGH)
      .setRequired(true);
  };

  addScaleItems_ = function (form, titles) {
    titles.forEach(function (title) {
      addScale17_(form, title);
    });
  };

  addSection_ = function (form, title) {
    return form.addSectionHeaderItem().setTitle(title);
  };

  addModuleV_ = function (form, title) {
    return addMultipleChoice_(form, title, PF3311_MODULE_V_OPTIONS);
  };

  addMeCueModuleII_ = function (form) {
    addSection_(form, 'meCUE — Módulo II (cualidades no instrumentales — avatar)');
    addScaleItems_(form, PF3311_MECUE_MODULE_II);
  };

  addRawTlxSection_ = function (form) {
    addSection_(form, 'Carga cognitiva (RAW-TLX adaptado)');
    addScaleItems_(form, PF3311_RAW_TLX);
  };

  logFormUrls_ = function (form, label) {
    Logger.log('=== ' + label + ' ===');
    Logger.log('Editar:    ' + form.getEditUrl());
    Logger.log('Responder: ' + form.getPublishedUrl());
  };
}

function createPF3311_Form3_PostC() {
  var form = FormApp.create('PF-3311 — Post bloque C (agente virtual)');
  configurePF3311Form_(form);

  form.setDescription(
    'Acabás de completar el bloque con AGENTE VIRTUAL (chat + personaje con voz).\n\n' +
    'Respondé según tu experiencia con ese sistema en este bloque. No hay respuestas correctas. Decidí de forma espontánea.\n\n' +
    '«El producto» = el agente virtual (asistencia + personaje) que acabás de usar.\n\n' +
    'Escala meCUE: 1 = Totalmente en desacuerdo · 7 = Totalmente de acuerdo.\n\n' +
    'Nota RAW-TLX: no había límite de tiempo impuesto en la aplicación; en carga cognitiva, respondé según cómo te sentiste.'
  );

  addParticipantCode_(form);

  addSection_(form, 'meCUE — Módulo I (cualidades instrumentales)');
  addScaleItems_(form, PF3311_MECUE_MODULE_I);

  addMeCueModuleII_(form);
  addSection_(form, 'meCUE — Módulo III (emociones)');
  addScaleItems_(form, PF3311_MECUE_MODULE_III);
  addSection_(form, 'meCUE — Módulo IV (consecuencias de uso — 2 ítems curados)');
  addScaleItems_(form, PF3311_MECUE_MODULE_IV);

  addSection_(form, 'meCUE — Módulo V (evaluación global)');
  addModuleV_(form,
    '¿Cómo evaluás el producto (el agente virtual) en general?'
  );

  addRawTlxSection_(form);

  logFormUrls_(form, 'Form3 Post C');
  return form;
}
