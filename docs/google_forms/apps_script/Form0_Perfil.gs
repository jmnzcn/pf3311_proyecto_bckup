/**
 * PF-3311 — Formulario 0: Perfil del participante
 * Ejecutar → createPF3311_Form0_Perfil
 * Este archivo es autocontenido (no requiere Common.gs).
 */
if (typeof configurePF3311Form_ === 'undefined') {
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

  addAgeRangeItem_ = function (form) {
    addMultipleChoice_(form, '¿En qué rango de edad te encontrás?', [
      '18–24 años',
      '25–34 años',
      '35–44 años',
      '45–54 años',
      '55–64 años',
      '65 años o más'
    ]);
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

  logFormUrls_ = function (form, label) {
    Logger.log('=== ' + label + ' ===');
    Logger.log('Editar:    ' + form.getEditUrl());
    Logger.log('Responder: ' + form.getPublishedUrl());
  };
}

function createPF3311_Form0_Perfil() {
  var form = FormApp.create('PF-3311 — Perfil del participante');
  configurePF3311Form_(form);

  form.setDescription(
    'Estudio: Efecto de la visibilidad de un agente virtual en el desempeño y la confianza del usuario.\n' +
    'Investigador: Ney Fred Jiménez Campos (B03230) — UCR.\n\n' +
    'Respondé con sinceridad. Tus respuestas son anónimas: usamos solo un código (P01, P02, etc.), no tu nombre.\n' +
    'Usá el mismo código que te indicó el investigador y que vas a ingresar en la aplicación Unity.'
  );

  addParticipantCode_(form);
  addAgeRangeItem_(form);

  addMultipleChoice_(form, 'Nivel educativo más alto alcanzado', [
    'Primaria completa',
    'Secundaria completa',
    'Técnico / diplomado',
    'Universidad incompleta',
    'Universidad completa (grado / licenciatura)',
    'Posgrado (maestría / doctorado)',
    'Otro'
  ]);

  addMultipleChoice_(form,
    '¿Con qué frecuencia usás asistentes digitales (Siri, Alexa, ChatGPT, Copilot, etc.)?',
    [
      'Nunca o casi nunca',
      'Algunas veces al mes',
      'Algunas veces por semana',
      'Casi todos los días',
      'Varias veces al día'
    ]
  );

  addMultipleChoice_(form,
    '¿Has interactuado antes con agentes virtuales o avatares conversacionales (videojuegos, apps, kioscos, etc.)?',
    [
      'Nunca',
      'Una o pocas veces',
      'Algunas veces',
      'Con frecuencia'
    ]
  );

  logFormUrls_(form, 'Form0 Perfil');
  return form;
}
