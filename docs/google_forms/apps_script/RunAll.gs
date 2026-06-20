/**
 * Crea los 4 formularios PF-3311 y escribe las URLs en el registro.
 * Ejecutar → createAllPF3311Forms
 */
function createAllPF3311Forms() {
  var forms = [
    createPF3311_Form0_Perfil(),
    createPF3311_Form1_PostA(),
    createPF3311_Form2_PostB(),
    createPF3311_Form3_PostC()
  ];

  Logger.log('');
  Logger.log('=== RESUMEN — Guardá estas URLs ===');
  forms.forEach(function (form, i) {
    Logger.log('Form' + i + ' Responder: ' + form.getPublishedUrl());
  });

  return forms;
}
