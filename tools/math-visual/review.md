# Review prompt

Шаблон запроса к модели с vision-входом. Подставьте имена кадров из `out/`.

---

You are reviewing how a Markdown viewer renders LaTeX formulas. Read the attached
image(s) with the Read tool (each path is absolute).

For EVERY formula you can see, report exactly these fields:

1. `formula` — the formula as printed (LaTeX-ish transcription).
2. `fallback` — YES if it is shown as monospace source text instead of a formula.
3. `overlap` — NO / YES, and if YES: does the formula touch or cross the
   paragraph text above or below it?
4. `size` — is the formula's height comparable to the body text (same x-height),
   or noticeably LARGER / SMALLER?
5. `overflow` — does the formula extend past the right edge of the text column,
   or is it cut off?
6. `nested_fractions` — for formulas containing fractions INSIDE a fraction
   denominator/numerator: are the inner numbers still readable, or are they
   tiny/blurry?
7. `spacing` — around `+`, `-`, `=` between tall stacked fractions: is there
   clear clearance, or do the signs touch the fraction bars?

Answer as a compact table, one row per formula. Then a single line:
`WORST: <the single most severe problem>`.
Do not give fix suggestions. Do not invent formulas that are not visible.
If an image cannot be read, say `IMAGE_UNREADABLE: <path>` and stop.
