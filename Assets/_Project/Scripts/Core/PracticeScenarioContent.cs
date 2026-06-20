using System.Collections.Generic;

namespace MyProject
{
    /// <summary>
    /// Dummy practice item — not used in experimental blocks A/B/C.
    /// </summary>
    public static class PracticeScenarioContent
    {
        public static QuestionManager.ExperimentQuestion CreateQuestion()
        {
            return new QuestionManager.ExperimentQuestion
            {
                situation =
                    "PRÁCTICA (no se registra)\r\n\r\n" +
                    "REGLAS\r\n" +
                    "• Lluvia intensa O viento mayor a 60 km/h → Actividad en interior\r\n" +
                    "• Temperatura mayor a 32 °C Y humedad mayor a 80 % → Actividad en interior\r\n" +
                    "• Si ninguna regla anterior aplica → Actividad en exterior\r\n\r\n" +
                    "SITUACIÓN\r\n" +
                    "Un grupo planea una actividad recreativa. Hay 28 °C, humedad 70 %, cielo despejado y viento leve.\r\n\r\n" +
                    "PREGUNTA\r\n" +
                    "¿Qué opción corresponde según las reglas?",
                optA = "Actividad en interior",
                optB = "Actividad en exterior",
                optC = "Cancelar la actividad",
                optD = "Elegir al azar",
                correctOption = "B"
            };
        }

        public static List<QuestionManager.ExperimentQuestion> CreateQuestionList()
        {
            return new List<QuestionManager.ExperimentQuestion> { CreateQuestion() };
        }
    }
}
