/**
 * PF-3311 — Formulario 1: Post bloque A (sin asistencia)
 * Ejecutar → createPF3311_Form1_PostA
 * Este archivo es autocontenido (no requiere Common.gs).
 */
if (typeof configurePF3311Form_ === 'undefined') {
  var PF3311_SCALE_LABEL_LOW = 'Totalmente en desacuerdo';
  var PF3311_SCALE_LABEL_HIGH = 'Totalmente de acuerdo';

  var PF3311_RAW_TLX = [
    'La tarea me exigió mucha actividad mental y concentración.',
    'Sentí presión de tiempo mientras resolvía los casos.',
    'Tuve que trabajar muy duro (esfuerzo) para completar la tarea.',
    'Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.',
    'Me sentí seguro/a de cómo desempeñé la tarea en este bloque.',
    'La tarea me resultó exigente en general.'
  ];

  configurePF3311Form_ = function (form) {
    form.setCollectEmail(false);
    return form;
  };

  addParticipantCode_ = function (form) {
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

function createPF3311_Form1_PostA() {
  var form = FormApp.create('PF-3311 — Post bloque A (sin asistencia)');
  configurePF3311Form_(form);

  form.setDescription(
    'Acabás de completar el bloque SIN asistencia del agente (solo casos de decisión).\n\n' +
    'En este cuestionario evaluamos la CARGA de la tarea, no un sistema de asistencia (porque en este bloque no hubo agente).\n\n' +
    'Escala para las preguntas siguientes: 1 = Totalmente en desacuerdo · 7 = Totalmente de acuerdo.\n\n' +
    'Nota: no había límite de tiempo impuesto en la aplicación; respondé según cómo te sentiste durante el bloque.'
  );

  addParticipantCode_(form);
  addRawTlxSection_(form);

  logFormUrls_(form, 'Form1 Post A');
  return form;
}
