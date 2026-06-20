# Reporte PF-3311

```text
PF-3311 - Notas de analisis (desde tablas exportadas)
Generado: 2026-06-18 01:03:56
Fuente: C:\Users\neyfr\Downloads\ExperimentPrototypeB03230\_analysis
========================================================================

=== Catalogo de sesiones ===

Sesiones encontradas: 5 | Canonicas: 5 | Completas (6+6+6): 5

Carpeta                                    P##      A   B   C   OK  Cons   Analisis
----------------------------------------------------------------------------------
P01_ID-2026061700000000-1000               P01      6   6   6   SI    SI         SI
    -> Unica carpeta para este participante
P02_ID-2026061700010000-1001               P02      6   6   6   SI    SI         SI
    -> Unica carpeta para este participante
P03_ID-2026061700020000-1002               P03      6   6   6   SI    SI         SI
    -> Unica carpeta para este participante
P04_ID-2026061700030000-1003               P04      6   6   6   SI    SI         SI
    -> Unica carpeta para este participante
P05_ID-2026061700040000-1004               P05      6   6   6   SI    SI         SI
    -> Unica carpeta para este participante

Completitud: 5/5 participantes canonicos con A+B+C (100.0%).

=== Perfil (Form 0) (perfil_participantes.csv) ===

ParticipantCode | AgeRange | Education | AssistantFrequency | AvatarExperience | SourceFile
------------------------------------------------
P01 | 25–34 años | Universidad completa (grado / licenciatura) | Algunas veces por semana | Algunas veces | Form0_Perfil.csv
P02 | 35–44 años | Posgrado (maestría / doctorado) | Casi todos los días | Con frecuencia | Form0_Perfil.csv
P03 | 18–24 años | Universidad incompleta | Varias veces al día | Una o pocas veces | Form0_Perfil.csv
P04 | 45–54 años | Técnico / diplomado | Algunas veces al mes | Nunca | Form0_Perfil.csv
P05 | 25–34 años | Universidad completa (grado / licenciatura) | Casi todos los días | Algunas veces | Form0_Perfil.csv

=== RQ1 - Precision (% aciertos por condicion) ===

Participante                        A        B        C     n(A/B/C)
--------------------------------------------------------------------
P01_ID-2026061700000000-1000    33.33    83.33    100.0 6/6/6
P02_ID-2026061700010000-1001    33.33    66.67    66.67 6/6/6
P03_ID-2026061700020000-1002     50.0    66.67    66.67 6/6/6
P04_ID-2026061700030000-1003     50.0    66.67    66.67 6/6/6
P05_ID-2026061700040000-1004    66.67    100.0    66.67 6/6/6

Media A: 46.67% (n=5)

Media B: 76.67% (n=5)

Media C: 73.33% (n=5)

Inferencia:
  Precision | Friedman chi2=7.6250, p=0.0221 (n=5)
  Kendall W = 0.763 (0 = sin acuerdo, 1 = ranking idéntico entre condiciones)
  Post hoc Wilcoxon pareado (Bonferroni y Holm, 3 comparaciones):
    A vs B: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875
    A vs C: W=0.0000, p=0.1250, p_adj_Bonf=0.3750, p_adj_Holm=0.2500
    B vs C: W=1.0000, p=1.0000, p_adj_Bonf=1.0000, p_adj_Holm=1.0000

=== RQ2 (Unity) - Confianza media (escala 1-7) ===

P01_ID-2026061700000000-1000 A=2.833 B=4.667 C=6.667
P02_ID-2026061700010000-1001 A=3.167 B=4.5 C=6.0
P03_ID-2026061700020000-1002 A=3.167 B=4.833 C=5.0
P04_ID-2026061700030000-1003 A=2.667 B=4.833 C=5.833
P05_ID-2026061700040000-1004 A=3.167 B=5.167 C=6.333
Media A: 3.0 (n=5)
Media B: 4.8 (n=5)
Media C: 5.967 (n=5)

Inferencia:
  Confianza Unity | Friedman chi2=10.0000, p=0.0067 (n=5)
  Kendall W = 1.000 (0 = sin acuerdo, 1 = ranking idéntico entre condiciones)
  Post hoc Wilcoxon pareado (Bonferroni y Holm, 3 comparaciones):
    A vs B: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875
    A vs C: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875
    B vs C: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875

=== RQ2 (Forms) - RAW-TLX ===

  P01: A=6.0 B=4.667 C=4.667
  P02: A=4.333 B=4.167 C=5.0
  P03: A=4.667 B=4.333 C=4.667
  P04: A=5.5 B=5.0 C=4.833
  P05: A=5.0 B=4.5 C=3.667

Inferencia:
  RAW-TLX | Friedman chi2=4.3333, p=0.1146 (n=5)
  Kendall W = 0.433 (0 = sin acuerdo, 1 = ranking idéntico entre condiciones)
  Post hoc Wilcoxon pareado (Bonferroni y Holm, 3 comparaciones):
    A vs B: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875
    A vs C: W=1.5000, p=0.3750, p_adj_Bonf=1.0000, p_adj_Holm=0.7500
    B vs C: W=4.5000, p=1.0000, p_adj_Bonf=1.0000, p_adj_Holm=1.0000

=== RQ2 (Forms) - meCUE B vs C ===

  Wilcoxon pareado B vs C por módulo meCUE (p ajustada Bonferroni, 4 comparaciones):
  Utilidad (Mód. I) | Wilcoxon B vs C: W=0.0000, p=0.0625, p_adj_Holm=0.2500
  Emociones (Mód. III) | Wilcoxon B vs C: W=1.0000, p=0.1250, p_adj_Holm=0.2500
  Consecuencias (Mód. IV) | Wilcoxon B vs C: W=0.0000, p=0.1250, p_adj_Holm=0.2500
  Evaluación global (Mód. V) | Wilcoxon B vs C: W=0.0000, p=0.0625, p_adj_Holm=0.2500

=== RQ3 - Calibracion (brecha confianza - precision) ===

P01_ID-2026061700000000-1000 A=-0.0278 B=-0.2222 C=-0.0556
P02_ID-2026061700010000-1001 A=0.0278 B=-0.0833 C=0.1667
P03_ID-2026061700020000-1002 A=-0.1389 B=-0.0278 C=0.0
P04_ID-2026061700030000-1003 A=-0.2222 B=-0.0278 C=0.1389
P05_ID-2026061700040000-1004 A=-0.3056 B=-0.3056 C=0.2222
Media A: brecha=-0.1333 (n=5)
Media B: brecha=-0.1333 (n=5)
Media C: brecha=0.0944 (n=5)

Inferencia:
  Brecha calibracion | Friedman chi2=5.1579, p=0.0759 (n=5)
  Kendall W = 0.516 (0 = sin acuerdo, 1 = ranking idéntico entre condiciones)
  Post hoc Wilcoxon pareado (Bonferroni y Holm, 3 comparaciones):
    A vs B: W=5.0000, p=1.0000, p_adj_Bonf=1.0000, p_adj_Holm=1.0000
    A vs C: W=1.0000, p=0.1250, p_adj_Bonf=0.3750, p_adj_Holm=0.2500
    B vs C: W=0.0000, p=0.0625, p_adj_Bonf=0.1875, p_adj_Holm=0.1875

=== Hipotesis exploratorias (medias grupales) ===

H1 (precision C ~ B > A): A=46.7% | B=76.7% | C=73.3%
  Orden observado (mayor a menor): B > C > A

H2 (confianza C > B > A): A=3.00 | B=4.80 | C=5.97
  Orden observado (mayor a menor): C > B > A

H3 (mayor brecha en C): A=-0.133 | B=-0.133 | C=0.094
  Orden observado (mayor brecha primero): C > A > B
  Brecha C vs A: consistente con H3

=== Metricas por item (precision) (items_accuracy.csv) ===

Condition | ScenarioNumber | QuestionNumber | N | AccuracyPct
----------------------------------------
A | 1 | 1 | 5 | 80.0
A | 1 | 2 | 5 | 40.0
A | 1 | 3 | 5 | 40.0
A | 1 | 4 | 5 | 20.0
A | 1 | 5 | 5 | 60.0
A | 1 | 6 | 5 | 40.0
B | 2 | 1 | 5 | 60.0
B | 2 | 2 | 5 | 60.0
B | 2 | 3 | 5 | 60.0
B | 2 | 4 | 5 | 100.0
B | 2 | 5 | 5 | 80.0
B | 2 | 6 | 5 | 100.0
C | 3 | 1 | 5 | 40.0
C | 3 | 2 | 5 | 100.0
C | 3 | 3 | 5 | 60.0
C | 3 | 4 | 5 | 80.0
C | 3 | 5 | 5 | 80.0
C | 3 | 6 | 5 | 80.0

=== RQ3 calibracion por item (rq3_calibration_by_item.csv) ===

Condition | QuestionNumber | ScenarioNumber | N | MeanConfidenceNorm | AccuracyPct | CalibrationGap
--------------------------------------------------------
A | 1 | 1 | 5 | 0.3667 | 80.0 | -0.4333
A | 2 | 1 | 5 | 0.3 | 40.0 | -0.1
A | 3 | 1 | 5 | 0.4333 | 40.0 | 0.0333
A | 4 | 1 | 5 | 0.3 | 20.0 | 0.1
A | 5 | 1 | 5 | 0.2333 | 60.0 | -0.3667
A | 6 | 1 | 5 | 0.3667 | 40.0 | -0.0333
B | 1 | 2 | 5 | 0.6333 | 60.0 | 0.0333
B | 2 | 2 | 5 | 0.6333 | 60.0 | 0.0333
B | 3 | 2 | 5 | 0.6 | 60.0 | 0.0
B | 4 | 2 | 5 | 0.5667 | 100.0 | -0.4333
B | 5 | 2 | 5 | 0.6 | 80.0 | -0.2
B | 6 | 2 | 5 | 0.7667 | 100.0 | -0.2333
C | 1 | 3 | 5 | 0.9333 | 40.0 | 0.5333
C | 2 | 3 | 5 | 0.8333 | 100.0 | -0.1667
C | 3 | 3 | 5 | 0.8667 | 60.0 | 0.2667
C | 4 | 3 | 5 | 0.8 | 80.0 | 0.0
C | 5 | 3 | 5 | 0.7333 | 80.0 | -0.0667
C | 6 | 3 | 5 | 0.8 | 80.0 | 0.0

=== Calidad del agente (B vs C) ===

P01    Help B=62.0 C=74.0 | Leaks B=0 C=0
P02    Help B=64.0 C=76.0 | Leaks B=0 C=1
P03    Help B=66.0 C=78.0 | Leaks B=1 C=0
P04    Help B=58.0 C=72.0 | Leaks B=0 C=0
P05    Help B=70.0 C=80.0 | Leaks B=0 C=0
  Media HelpScore: B=64.0 (n=5) | C=76.0 (n=5)
  Media Engagement: B=56.6 (n=5) | C=66.0 (n=5)
  Media ChatExchanges: B=4.2 (n=5) | C=5.6 (n=5)
  Media ModelLeaks: B=0.2 (n=5) | C=0.2 (n=5)
    Wilcoxon HelpScore | Wilcoxon B vs C: W=0.0000, p=0.0625, p_adj_Holm=0.0625
    Wilcoxon ChatExchanges | Wilcoxon B vs C: W=0.0000, p=0.0625, p_adj_Holm=0.0625
    Wilcoxon ModelLeaks | Wilcoxon B vs C: W=1.5000, p=1.0000, p_adj_Holm=1.0000

=== Viabilidad técnica del estudio (resumen) ===

  ValidRows: 90
  TotalRows: 90
  ValidPct: 100.0
  Passes95PctRule: 1
  GeminiPasses90Pct: 1
  TtsPasses85Pct: 0
  TotalModelLeaks: 1
  LeaksPer10Participants: 2.0
  LeaksPassRule: 1

=== Latencia Gemini (pilot_gemini_latency.csv) ===

SessionFolder | Turns | WithinThreshold | PctWithinThreshold | MeanSeconds | ModelLeakCount | Passes90PctRule
--------------------------------------------------------
P01_ID-2026061700000000-1000 | 11 | 11 | 100.0 | 3.35 | 0 | 1
P02_ID-2026061700010000-1001 | 9 | 9 | 100.0 | 2.842 | 0 | 1
P03_ID-2026061700020000-1002 | 6 | 6 | 100.0 | 3.372 | 1 | 1
P04_ID-2026061700030000-1003 | 9 | 9 | 100.0 | 3.527 | 0 | 1
P05_ID-2026061700040000-1004 | 9 | 9 | 100.0 | 2.94 | 0 | 1

=== TTS condicion C (pilot_tts_success.csv) ===

SessionFolder | Attempts | Successes | SuccessRatePct | Passes85PctRule
----------------------------------------
P01_ID-2026061700000000-1000 | 5 | 4 | 80.0 | 0
P02_ID-2026061700010000-1001 | 5 | 5 | 100.0 | 1
P03_ID-2026061700020000-1002 | 3 | 3 | 100.0 | 1
P04_ID-2026061700030000-1003 | 5 | 3 | 60.0 | 0
P05_ID-2026061700040000-1004 | 3 | 2 | 66.67 | 0

=== Graficos disponibles ===

  figures/chat_engagement_b_vs_c.png
  figures/chat_exchanges_b_vs_c.png
  figures/chat_helpscore_b_vs_c.png
  figures/chat_leaks_b_vs_c.png
  figures/forms_mecue_b_vs_c.png
  figures/forms_raw_tlx_by_condition.png
  figures/items_time_by_condition.png
  figures/perfil_muestra.png
  figures/rq1_precision.png
  figures/rq2_confidence.png
  figures/rq3_calibration_curve_by_item.png
  figures/rq3_calibration_gap.png
  figures/rq3_calibration_gap_by_item.png
```
