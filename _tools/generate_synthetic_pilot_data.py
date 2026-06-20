#!/usr/bin/env python3
"""
Genera datos sintéticos de piloto (hasta 15+ participantes) para probar gráficos y análisis.

Crea carpetas en CSV data/P##_ID-.../ y exportaciones en Forms data/.
Flujo completo: Form 0 (perfil) + Post A/B/C + sesión Unity (18 ítems + chat/TTS).

Priors alineados al Entregable 2 (simulación, no datos reales):
  H1 — precisión:  C ≈ B > A
  H2 — confianza:  C > B > A
  H3 — calibración: mayor brecha confianza–precisión en C (confianza alta con precisión similar a B)

Uso:
  python _tools/generate_synthetic_pilot_data.py --participants 15 --clean --run-analysis
"""

from __future__ import annotations

import argparse
import csv
import random
import shutil
import subprocess
import sys
from dataclasses import dataclass, replace
from datetime import datetime, timedelta
from pathlib import Path

_TOOLS = Path(__file__).resolve().parent
ROOT = _TOOLS.parent
if str(_TOOLS) not in sys.path:
    sys.path.insert(0, str(_TOOLS))

from forms_export_schema import (
    AGE_OPTIONS,
    ASSISTANT_FREQ_OPTIONS,
    AVATAR_EXP_OPTIONS,
    EDUCATION_OPTIONS,
    FORM0_PERFIL_HEADERS,
    FORM_POST_A_HEADERS,
    FORM_POST_B_HEADERS,
    FORM_POST_C_HEADERS,
    MECUE_V_LABELS,
)

DEFAULT_PARTICIPANTS = 15
MAX_PARTICIPANTS = 30

EXPERIMENT_HEADER = (
    "ParticipantCode,SessionID,ScenarioNumber,ScenarioName,QuestionNumber,"
    "User_answer_Letter,User_answer,CorrectAnswerLetter,CorrectAnswer,Confidence,TimeSpent(Seconds),Timestamp\n"
)
CONSENT_HEADER = "ParticipantCode,SessionID,AgeConsent,StudyConsent,ConsentFormVersion,Timestamp\n"
CHAT_SCENARIO_HEADER = (
    "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,TotalQuestions,"
    "QuestionsWithChat,TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,"
    "AnswerRequestCount,OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,"
    "SubstantiveQuestionCount,ChatUsedInBlock,ChatMeetsProtocol,DominantUtilityLevel,"
    "TtsAttempts,TtsSuccessCount,TtsSuccessRate,EffectiveHelpLevel,Flags,Timestamp\n"
)
CHAT_QUESTION_HEADER = (
    "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,"
    "TotalExchanges,TotalTurns,AvgHelpScore,AvgTaskEngagementScore,AnswerRequestCount,"
    "OffTopicCount,ModelLeakCount,OnTopicSeconds,OffTopicSeconds,SubstantiveQuestionCount,"
    "ChatUsedInQuestion,DominantUtilityLevel,EffectiveHelpLevel,Flags,Timestamp\n"
)
CHAT_HELP_HEADER = (
    "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,"
    "ExchangeIndex,StudentMessage,ModelMessage,HelpScore,GuidanceScore,TaskEngagementScore,"
    "StudentRequestedAnswer,StudentOffTopic,StudentGamingAttempt,ModelPossibleLeak,"
    "ScenarioRelevanceScore,SubstantiveQuestion,QuestionUtilityLevel,HelpLevel,Flags,"
    "SecondsSinceQuestionStart,GeminiLatencySeconds,Timestamp\n"
)
TTS_HEADER = (
    "ParticipantCode,SessionID,ConditionCode,ScenarioNumber,ScenarioName,QuestionNumber,"
    "ExchangeIndex,TtsSuccess,FailureReason,Timestamp\n"
)

SCENARIO_NAMES = {
    1: "Bloque A - Sin agente",
    2: "Bloque B - Chat texto",
    3: "Bloque C - Avatar y voz",
}
CORRECT_LETTERS = ("A", "C", "B", "D", "A", "B")
OPTION_TEXT = {
    "A": "Opcion A",
    "B": "Opcion B",
    "C": "Opcion C",
    "D": "Opcion D",
}
WRONG_FOR = {"A": "B", "C": "D", "B": "A", "D": "C"}


@dataclass(frozen=True)
class ParticipantProfile:
    code: str
    accuracy: tuple[float, float, float]  # A, B, C
    confidence: tuple[tuple[int, int], tuple[int, int], tuple[int, int]]
    help_score: tuple[float, float]  # B, C
    engagement: tuple[float, float]
    exchanges: tuple[int, int]
    leaks: tuple[int, int]
    tlx: tuple[float, float, float]
    mecue_i: tuple[float, float]  # B, C
    mecue_ii: tuple[float, float, float, float, float]  # C — módulo II (5 ítems)
    mecue_iii: tuple[float, float]
    mecue_global: tuple[int, int]  # módulo V (−5…+5)
    age: str
    education: str
    assistant_freq: str
    avatar_exp: str


# Plantillas calibradas: H1 (C≈B>A), H2 (C>B>A), meCUE/HelpScore C≥B.
PROFILES: list[ParticipantProfile] = [
    ParticipantProfile(
        "P01", (0.50, 0.78, 0.78), ((2, 4), (4, 5), (5, 7)),
        (62.0, 74.0), (55.0, 64.0), (4, 5), (0, 0), (5.2, 4.6, 4.8),
        (4.8, 5.5), (5.0, 5.2, 4.9, 5.4, 5.0), (4.2, 5.0), (0, 2),
        "25–34 años", "Universidad completa (grado / licenciatura)",
        "Algunas veces por semana", "Algunas veces",
    ),
    ParticipantProfile(
        "P02", (0.55, 0.72, 0.78), ((3, 4), (4, 6), (5, 7)),
        (64.0, 76.0), (56.0, 66.0), (5, 6), (0, 1), (5.0, 4.4, 4.6),
        (4.9, 5.6), (5.1, 5.3, 5.0, 5.5, 5.1), (4.4, 5.1), (0, 1),
        "35–44 años", "Posgrado (maestría / doctorado)",
        "Casi todos los días", "Con frecuencia",
    ),
    ParticipantProfile(
        "P03", (0.58, 0.83, 0.78), ((2, 4), (4, 5), (5, 6)),
        (66.0, 78.0), (58.0, 68.0), (3, 5), (1, 0), (4.8, 4.2, 4.4),
        (5.0, 5.7), (5.0, 5.4, 5.1, 5.6, 5.2), (4.6, 5.2), (1, 2),
        "18–24 años", "Universidad incompleta",
        "Varias veces al día", "Una o pocas veces",
    ),
    ParticipantProfile(
        "P04", (0.50, 0.75, 0.75), ((3, 4), (4, 5), (5, 7)),
        (58.0, 72.0), (52.0, 62.0), (3, 5), (0, 0), (5.4, 4.8, 5.0),
        (4.6, 5.3), (4.8, 5.0, 4.7, 5.2, 4.9), (4.0, 4.8), (-1, 1),
        "45–54 años", "Técnico / diplomado",
        "Algunas veces al mes", "Nunca",
    ),
    ParticipantProfile(
        "P05", (0.55, 0.83, 0.78), ((3, 4), (5, 6), (6, 7)),
        (70.0, 80.0), (62.0, 70.0), (6, 7), (0, 0), (4.6, 4.0, 4.2),
        (5.2, 5.8), (5.3, 5.5, 5.4, 5.7, 5.3), (4.8, 5.3), (1, 3),
        "25–34 años", "Universidad completa (grado / licenciatura)",
        "Casi todos los días", "Algunas veces",
    ),
    ParticipantProfile(
        "P06", (0.55, 0.78, 0.83), ((2, 3), (4, 5), (5, 7)),
        (60.0, 74.0), (54.0, 64.0), (4, 6), (1, 0), (4.4, 4.0, 4.3),
        (4.8, 5.4), (5.1, 5.3, 5.0, 5.5, 5.0), (4.3, 5.0), (0, 2),
        "55–64 años", "Secundaria completa",
        "Nunca o casi nunca", "Una o pocas veces",
    ),
]


def _clamp01(value: float) -> float:
    return max(0.33, min(1.0, value))


def _conf_mid(bounds: tuple[int, int]) -> float:
    lo, hi = bounds
    return (lo + hi) / 2.0


def _align_confidence(
    bounds: tuple[tuple[int, int], tuple[int, int], tuple[int, int]],
) -> tuple[tuple[int, int], tuple[int, int], tuple[int, int]]:
    """H2: centros A < B < C, con rangos de 1–7."""
    a, b, c = bounds
    a_mid = max(2.0, min(4.5, _conf_mid(a)))
    b_mid = max(a_mid + 0.8, min(6.0, _conf_mid(b)))
    c_mid = max(b_mid + 0.8, min(7.0, _conf_mid(c)))

    def _band(mid: float, spread: int = 1) -> tuple[int, int]:
        lo = max(1, int(round(mid - spread)))
        hi = min(7, int(round(mid + spread)))
        if lo > hi:
            lo = hi
        return lo, hi

    return _band(a_mid), _band(b_mid), _band(c_mid)


def _align_accuracy(acc: tuple[float, float, float]) -> tuple[float, float, float]:
    """H1: A claramente menor; B y C cercanas (±0.10)."""
    a, b, c = _clamp01(acc[0]), _clamp01(acc[1]), _clamp01(acc[2])
    agent_mid = (b + c) / 2.0
    agent_mid = _clamp01(agent_mid)
    delta = max(-0.08, min(0.08, b - c))
    b = _clamp01(agent_mid + delta / 2)
    c = _clamp01(agent_mid - delta / 2)
    ceiling = min(b, c)
    a = min(a, _clamp01(ceiling - 0.14))
    a = max(0.33, a)
    return (round(a, 2), round(b, 2), round(c, 2))


def _align_agent_scores(
    help_score: tuple[float, float],
    engagement: tuple[float, float],
    mecue_i: tuple[float, float],
    mecue_global: tuple[int, int],
) -> tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[int, int]]:
    """C ≥ B en percepción del agente (HelpScore, meCUE)."""
    help_b, help_c = help_score
    eng_b, eng_c = engagement
    mecue_b, mecue_c = mecue_i
    glob_b, glob_c = mecue_global

    help_b = max(50.0, min(88.0, help_b))
    help_c = max(help_b + 4.0, min(92.0, help_c))
    eng_b = max(45.0, min(85.0, eng_b))
    eng_c = max(eng_b + 2.0, min(90.0, eng_c))
    mecue_b = max(3.5, min(6.5, mecue_b))
    mecue_c = max(mecue_b + 0.3, min(7.0, mecue_c))
    glob_c = max(glob_b + 1, min(3, glob_c))

    return (round(help_b, 1), round(help_c, 1)), (round(eng_b, 1), round(eng_c, 1)), (
        round(mecue_b, 1),
        round(mecue_c, 1),
    ), (glob_b, glob_c)


def _align_profile(profile: ParticipantProfile) -> ParticipantProfile:
    acc = _align_accuracy(profile.accuracy)
    conf = _align_confidence(profile.confidence)
    help_score, engagement, mecue_i, mecue_global = _align_agent_scores(
        profile.help_score,
        profile.engagement,
        profile.mecue_i,
        profile.mecue_global,
    )
    tlx_a, tlx_b, tlx_c = profile.tlx
    tlx_a = max(4.2, min(6.0, tlx_a))
    tlx_b = max(3.5, min(5.5, min(tlx_a - 0.3, tlx_b)))
    tlx_c = max(3.8, min(5.8, tlx_c))
    return replace(
        profile,
        accuracy=acc,
        confidence=conf,
        help_score=help_score,
        engagement=engagement,
        mecue_i=mecue_i,
        mecue_global=mecue_global,
        tlx=(round(tlx_a, 1), round(tlx_b, 1), round(tlx_c, 1)),
    )


def _vary_profile(base: ParticipantProfile, code: str, index: int, rng: random.Random) -> ParticipantProfile:
    jitter = (index % 5) * 0.015
    acc = tuple(
        _clamp01(a + rng.uniform(-0.06, 0.06) + (jitter if i == 0 else 0.0))
        for i, a in enumerate(base.accuracy)
    )
    conf = tuple(
        sorted(
            (
                max(1, min(7, lo + rng.randint(-1, 1))),
                max(1, min(7, hi + rng.randint(-1, 1))),
            )
        )
        for lo, hi in base.confidence
    )
    help_b = base.help_score[0] + rng.uniform(-5, 5)
    help_c = base.help_score[1] + rng.uniform(-5, 5)
    tlx = tuple(max(2.5, min(6.5, t + rng.uniform(-0.35, 0.35))) for t in base.tlx)
    mecue_i = (
        max(3.0, min(7.0, base.mecue_i[0] + rng.uniform(-0.4, 0.4))),
        max(3.0, min(7.0, base.mecue_i[1] + rng.uniform(-0.4, 0.4))),
    )
    mecue_ii = tuple(max(3.0, min(7.0, v + rng.uniform(-0.5, 0.5))) for v in base.mecue_ii)
    mecue_iii = (
        max(3.0, min(7.0, base.mecue_iii[0] + rng.uniform(-0.5, 0.5))),
        max(3.0, min(7.0, base.mecue_iii[1] + rng.uniform(-0.5, 0.5))),
    )
    mod_v = (
        max(-3, min(3, base.mecue_global[0] + rng.randint(-1, 1))),
        max(-3, min(3, base.mecue_global[1] + rng.randint(-1, 1))),
    )
    profile = ParticipantProfile(
        code=code,
        accuracy=acc,  # type: ignore[arg-type]
        confidence=conf,  # type: ignore[arg-type]
        help_score=(round(help_b, 1), round(help_c, 1)),
        engagement=(
            max(40.0, min(90.0, base.engagement[0] + rng.uniform(-4, 4))),
            max(40.0, min(90.0, base.engagement[1] + rng.uniform(-4, 4))),
        ),
        exchanges=(
            max(2, min(8, base.exchanges[0] + rng.randint(-1, 1))),
            max(2, min(8, base.exchanges[1] + rng.randint(-1, 1))),
        ),
        leaks=(
            max(0, min(2, base.leaks[0] + (1 if rng.random() < 0.10 else 0))),
            max(0, min(2, base.leaks[1] + (1 if rng.random() < 0.06 else 0))),
        ),
        tlx=tlx,  # type: ignore[arg-type]
        mecue_i=mecue_i,  # type: ignore[arg-type]
        mecue_ii=mecue_ii,  # type: ignore[arg-type]
        mecue_iii=mecue_iii,  # type: ignore[arg-type]
        mecue_global=mod_v,
        age=AGE_OPTIONS[(index - 1) % len(AGE_OPTIONS)],
        education=EDUCATION_OPTIONS[(index + 1) % len(EDUCATION_OPTIONS)],
        assistant_freq=ASSISTANT_FREQ_OPTIONS[(index + 2) % len(ASSISTANT_FREQ_OPTIONS)],
        avatar_exp=AVATAR_EXP_OPTIONS[(index + 3) % len(AVATAR_EXP_OPTIONS)],
    )
    return _align_profile(profile)


def build_profiles(count: int, seed: int) -> list[ParticipantProfile]:
    count = max(1, min(count, MAX_PARTICIPANTS))
    rng = random.Random(seed + 99)
    profiles: list[ParticipantProfile] = []
    for index in range(1, count + 1):
        code = f"P{index:02d}"
        if index <= len(PROFILES):
            profile = PROFILES[index - 1]
            profiles.append(_align_profile(replace(profile, code=code)))
        else:
            template = PROFILES[(index - 1) % len(PROFILES)]
            profiles.append(_vary_profile(template, code, index, rng))
    return profiles


def _clean_synthetic_sessions(csv_dir: Path) -> int:
    removed = 0
    for path in sorted(csv_dir.glob("P*_ID-*")):
        if path.is_dir():
            shutil.rmtree(path)
            removed += 1
    return removed


def _ts(base: datetime, minutes: int) -> str:
    return (base + timedelta(minutes=minutes)).strftime("%Y-%m-%d %H:%M:%S")


def _write_csv(path: Path, header: str | list[str], rows: list[list[object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        if isinstance(header, str):
            handle.write(header)
        else:
            writer.writerow(header)
        writer.writerows(rows)


def _pick_answer(correct: str, accuracy: float, rng: random.Random) -> str:
    return correct if rng.random() < accuracy else WRONG_FOR[correct]


def _build_experiment_rows(profile: ParticipantProfile, session_id: str, base: datetime, rng: random.Random) -> list[list[object]]:
    rows: list[list[object]] = []
    minute = 0
    for scenario in (1, 2, 3):
        cond_idx = scenario - 1
        accuracy = profile.accuracy[cond_idx]
        conf_lo, conf_hi = profile.confidence[cond_idx]
        lo, hi = min(conf_lo, conf_hi), max(conf_lo, conf_hi)
        for q in range(1, 7):
            correct = CORRECT_LETTERS[q - 1]
            answer = _pick_answer(correct, accuracy, rng)
            confidence = rng.randint(lo, hi)
            time_spent = round(rng.uniform(18.0, 95.0), 1)
            minute += 1
            rows.append(
                [
                    profile.code,
                    session_id,
                    scenario,
                    SCENARIO_NAMES[scenario],
                    q,
                    answer,
                    OPTION_TEXT[answer],
                    correct,
                    OPTION_TEXT[correct],
                    confidence,
                    time_spent,
                    _ts(base, minute),
                ]
            )
    return rows


def _build_chat_files(
    profile: ParticipantProfile,
    session_id: str,
    base: datetime,
    rng: random.Random,
) -> dict[str, tuple[str, list[list[object]]]]:
    scenario_rows: list[list[object]] = []
    question_rows: list[list[object]] = []
    help_rows: list[list[object]] = []
    tts_rows: list[list[object]] = []
    minute = 30

    for scenario, cond in ((2, "B"), (3, "C")):
        idx = 0 if cond == "B" else 1
        help = profile.help_score[idx]
        engagement = profile.engagement[idx]
        exchanges = profile.exchanges[idx]
        leaks = profile.leaks[idx]
        questions_with_chat = rng.randint(3, 6)
        tts_attempts = exchanges if cond == "C" else 0
        tts_success = max(0, tts_attempts - (1 if cond == "C" and rng.random() < 0.08 else 0))
        tts_rate = round(100.0 * tts_success / tts_attempts, 1) if tts_attempts else ""

        on_topic = round(rng.uniform(30, 120), 1) if exchanges else 0.0
        off_topic = round(rng.uniform(0, 25), 1) if exchanges else 0.0
        substantive = max(0, questions_with_chat - rng.randint(0, 1))
        utility = "Productive" if help >= 65 else "Minimal"

        scenario_rows.append(
            [
                profile.code,
                session_id,
                cond,
                scenario,
                SCENARIO_NAMES[scenario],
                6,
                questions_with_chat,
                exchanges,
                exchanges * 2,
                round(help, 1),
                round(engagement, 1),
                rng.randint(0, 1),
                rng.randint(0, 1),
                leaks,
                on_topic,
                off_topic,
                substantive,
                "1" if questions_with_chat > 0 else "0",
                "1" if exchanges >= 1 else "0",
                utility,
                tts_attempts,
                tts_success,
                tts_rate,
                "Moderate" if help >= 65 else "Low",
                "",
                _ts(base, minute),
            ]
        )
        minute += 1

        for q in range(1, 7):
            q_ex = 1 if q <= questions_with_chat else 0
            q_help = round(help + rng.uniform(-8, 8), 1) if q_ex else ""
            q_leak = 1 if leaks > 0 and q == 2 and cond == "B" else 0
            q_on = round(rng.uniform(5, 35), 1) if q_ex else 0.0
            q_off = round(rng.uniform(0, 12), 1) if q_ex else 0.0
            question_rows.append(
                [
                    profile.code,
                    session_id,
                    cond,
                    scenario,
                    SCENARIO_NAMES[scenario],
                    q,
                    q_ex,
                    q_ex * 2,
                    q_help,
                    round(engagement + rng.uniform(-5, 5), 1) if q_ex else "",
                    0,
                    0,
                    q_leak,
                    q_on,
                    q_off,
                    1 if q_ex else 0,
                    "1" if q_ex else "0",
                    "Productive" if q_ex else "None",
                    "Moderate" if q_ex else "None",
                    "",
                    _ts(base, minute),
                ]
            )
            minute += 1

            if q_ex:
                latency = round(rng.uniform(1.8, 4.5), 2)
                seconds_start = round(rng.uniform(8, 40), 1)
                help_rows.append(
                    [
                        profile.code,
                        session_id,
                        cond,
                        scenario,
                        SCENARIO_NAMES[scenario],
                        q,
                        1,
                        "¿Puedo aplicar la regla 2?",
                        "Pense en el orden de las condiciones.",
                        round(help + rng.uniform(-5, 5), 1),
                        round(help, 1),
                        round(engagement, 1),
                        0,
                        0,
                        0,
                        1 if q_leak else 0,
                        round(rng.uniform(45, 85), 1),
                        1,
                        "Productive",
                        "Moderate",
                        "",
                        seconds_start,
                        latency,
                        _ts(base, minute),
                    ]
                )
                if cond == "C":
                    tts_rows.append(
                        [
                            profile.code,
                            session_id,
                            cond,
                            scenario,
                            SCENARIO_NAMES[scenario],
                            q,
                            1,
                            1 if rng.random() > 0.06 else 0,
                            "" if rng.random() > 0.06 else "timeout",
                            _ts(base, minute),
                        ]
                    )

    return {
        "ChatScenarioSummary.csv": (CHAT_SCENARIO_HEADER, scenario_rows),
        "ChatQuestionSummary.csv": (CHAT_QUESTION_HEADER, question_rows),
        "ChatHelpRating.csv": (CHAT_HELP_HEADER, help_rows),
        "TtsLog.csv": (TTS_HEADER, tts_rows),
    }


def _scale_value(center: float, rng: random.Random) -> int:
    value = int(round(center + rng.uniform(-1.2, 1.2)))
    return max(1, min(7, value))


def _scale_values(center: float, count: int, rng: random.Random, spread: float = 1.0) -> list[int]:
    return [_scale_value(center + rng.uniform(-spread, spread), rng) for _ in range(count)]


def _mod_v_label(value: int) -> str:
    return MECUE_V_LABELS.get(value, "0 — Neutral")


def _form_row_perfil(profile: ParticipantProfile, rng: random.Random) -> list[object]:
    return [
        _ts(datetime(2026, 6, 16, 8, 30), rng.randint(0, 30)),
        profile.code,
        profile.age,
        profile.education,
        profile.assistant_freq,
        profile.avatar_exp,
    ]


def _form_row_post_a(profile: ParticipantProfile, rng: random.Random) -> list[object]:
    return [
        _ts(datetime(2026, 6, 16, 10, 0), rng.randint(0, 120)),
        profile.code,
        *_scale_values(profile.tlx[0], 6, rng),
    ]


def _form_row_post_bc(profile: ParticipantProfile, condition: str, rng: random.Random) -> list[object]:
    idx = {"B": 1, "C": 2}[condition]
    row: list[object] = [
        _ts(datetime(2026, 6, 16, 11, 0), rng.randint(0, 180)),
        profile.code,
        *_scale_values(profile.mecue_i[idx - 1], 6, rng, spread=0.8),
    ]
    if condition == "C":
        for center in profile.mecue_ii:
            row.append(_scale_value(center + rng.uniform(-0.7, 0.7), rng))
    row.extend(_scale_values(profile.mecue_iii[idx - 1], 12, rng, spread=1.0))
    row.extend(_scale_values(5.0, 2, rng, spread=0.8))
    row.append(_mod_v_label(profile.mecue_global[idx - 1]))
    row.extend(_scale_values(profile.tlx[idx], 6, rng, spread=0.5))
    return row


def generate_forms(profiles: list[ParticipantProfile], forms_dir: Path, rng: random.Random) -> None:
    forms_dir.mkdir(parents=True, exist_ok=True)

    _write_csv(
        forms_dir / "Form0_Perfil.csv",
        list(FORM0_PERFIL_HEADERS),
        [_form_row_perfil(p, rng) for p in profiles],
    )
    _write_csv(
        forms_dir / "PostBloqueA.csv",
        list(FORM_POST_A_HEADERS),
        [_form_row_post_a(p, rng) for p in profiles],
    )
    _write_csv(
        forms_dir / "PostBloqueB.csv",
        list(FORM_POST_B_HEADERS),
        [_form_row_post_bc(p, "B", rng) for p in profiles],
    )
    _write_csv(
        forms_dir / "PostBloqueC.csv",
        list(FORM_POST_C_HEADERS),
        [_form_row_post_bc(p, "C", rng) for p in profiles],
    )


def generate_participant(
    profile: ParticipantProfile,
    csv_dir: Path,
    rng: random.Random,
    index: int,
) -> Path:
    session_id = f"ID-20260617{index:04d}0000-{1000 + index:04d}"
    folder = csv_dir / f"{profile.code}_{session_id}"
    base = datetime(2026, 6, 17, 8, 0) + timedelta(minutes=index * 12)

    _write_csv(
        folder / "ConsentLog.csv",
        CONSENT_HEADER,
        [[profile.code, session_id, 1, 1, "v1.0-piloto", _ts(base, 0)]],
    )
    _write_csv(
        folder / "ExperimentData.csv",
        EXPERIMENT_HEADER,
        _build_experiment_rows(profile, session_id, base, rng),
    )

    for filename, (header, rows) in _build_chat_files(profile, session_id, base, rng).items():
        if rows:
            _write_csv(folder / filename, header, rows)

    return folder


def main() -> int:
    parser = argparse.ArgumentParser(description="Genera datos sintéticos P01..Pn para el piloto")
    parser.add_argument("--csv-dir", type=Path, default=ROOT / "CSV data")
    parser.add_argument("--forms-dir", type=Path, default=ROOT / "Forms data")
    parser.add_argument(
        "--participants",
        type=int,
        default=DEFAULT_PARTICIPANTS,
        help=f"Número de participantes (1–{MAX_PARTICIPANTS}, default {DEFAULT_PARTICIPANTS})",
    )
    parser.add_argument("--seed", type=int, default=3311)
    parser.add_argument(
        "--clean",
        action="store_true",
        help="Borra carpetas P##_ID-* previas en CSV data/",
    )
    parser.add_argument("--run-analysis", action="store_true", help="Ejecuta analyze_all_rq.py al terminar")
    args = parser.parse_args()

    profiles = build_profiles(args.participants, args.seed)
    rng = random.Random(args.seed)

    csv_dir = args.csv_dir
    csv_dir.mkdir(parents=True, exist_ok=True)

    if args.clean:
        removed = _clean_synthetic_sessions(csv_dir)
        print(f"Eliminadas {removed} carpetas sintéticas en {csv_dir}")

    folders: list[Path] = []
    for index, profile in enumerate(profiles):
        folder = generate_participant(profile, csv_dir, rng, index)
        folders.append(folder)
        print(f"  {folder.name}")

    generate_forms(profiles, args.forms_dir, rng)
    print(f"\n{len(profiles)} sesiones en {csv_dir.resolve()}")
    print(
        f"Forms: {args.forms_dir.resolve()} "
        f"(Form0_Perfil + PostBloqueA/B/C.csv, {len(profiles)} filas c/u)"
    )

    if args.run_analysis:
        cmd = [
            sys.executable,
            str(_TOOLS / "analyze_all_rq.py"),
            str(csv_dir),
            "--forms-dir",
            str(args.forms_dir),
        ]
        print("\nEjecutando pipeline de análisis...")
        return subprocess.call(cmd, cwd=ROOT)

    print("\nSiguiente paso:")
    print(f'  python _tools/analyze_all_rq.py "{csv_dir}" --forms-dir "{args.forms_dir}"')
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
