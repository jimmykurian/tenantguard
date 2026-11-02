# Accessibility

TenantGuard is a set of libraries, so what people read from it is text that other tools
show: exception messages, log messages, compiler diagnostics in an editor or a build log,
and the isolation probe's report in a test run. Accessibility here means that text works
with a screen reader, a braille display, screen magnification and other tools, and that the
docs read well with them. This file sets the goal, records where the project stands, and
lists the rules that keep it there.

## The goal

Everything TenantGuard says can be understood without seeing color, layout or symbols, and
every result is also available in a structured form that other tools can present however
their users need.

For criteria, the project follows the W3C's [guidance on applying WCAG to non-web
software][wcag2ict] where it fits a library: meaning that doesn't depend on color or sensory
cues (1.3.3, 1.4.1), text that can be read in order (1.3.2), and errors that are identified
and described in text (3.3.1). The docs site aims at [WCAG 2.2][wcag22] level AA.

## Where it stands

| Area | Status |
| --- | --- |
| Color | None: TenantGuard writes no color or ANSI escape codes. Its text goes through exceptions, `ILogger` and the compiler, and how those are shown is up to the host, the logger provider or the editor |
| Severity | Carried by words: log levels, the error and warning severity of each analyzer diagnostic and model finding, and `Passed`, `Failed` and `Skipped` in the probe report, each with a rule ID (`TG001`, `TGM001`) or a check name (`ReadIsolation`) |
| Symbols | None in its own text: no icons, box drawing or progress bars. The probe's table rules its header with hyphens |
| Motion and timing | None in the libraries. The two animated terminal demos in `docs/assets/` stop animating and show every line at once under `prefers-reduced-motion`, and each carries a text description of what it shows |
| Structured output | Every finding is data as well as text: `TenantGuardModelException.Report` holds every model finding, `TenantIsolationViolationException.Violations` its blocked writes, `ProbeReport.Rows` each check's outcome, and each log message has an event ID and named placeholders ([Logging](docs/reference/logging.md)) |
| Errors | Full sentences that name the entity, the property and the fix (`Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.`) |
| Pull requests | The check table says each result in a word (`Passed`, `Failed`, `Skipped`, `Not run`); its symbol only repeats it |
| Docs | Markdown with headings in order, tables with header rows, and alt text on every image, published as an Astro Starlight site that has a skip link, landmarks, keyboard navigation and a light and a dark theme |

## Known gaps

1. **The probe's table is laid out with spaces.** `AssertAllPassed()` throws a fixed-width
   table, so a screen reader reads it a line at a time, without the column headers on each
   row. Workaround: `ProbeReport.Rows`, which a test can assert on or print however it
   needs. Changing the message is a contract change
   ([Compatibility](docs/reference/compatibility.md)).
2. **Diagrams have no text alternative of their own.** The Mermaid diagrams in `docs/`
   render as images, and the prose around each summarizes it but not every step. The home
   page's leak diagram is repeated as the table below it.
3. **The README's status badges show a result only in the image.** Their alt text names the
   check, not whether it passed. Workaround: the workflow runs they link to, or a pull
   request's check table.
4. **No record of manual checks.** The checklist below hasn't been run and recorded for a
   release yet, and the docs site's theme hasn't been checked for contrast.

## Rules for changes

These apply to every change to messages, output or docs:

- **Never convey meaning by color, position or a symbol alone.** Any new state gets a word
  in the text.
- **Messages are sentences.** Name the entity, the property and the rule in words, and say
  what to do next, as the existing rules do. A message reads on its own, without the code
  that raised it.
- **Errors say what and where.** Name the entity type, the property, the key values and the
  tenants involved, never other entity values.
- **Keep the data complete.** Anything a message shows is also on the exception, the report
  or the log entry as data, so other tools can present it differently.
- **Docs:** headings in order, tables with a header row, a language on every code block,
  link text that says where it goes, alt text on every image, and a text description and
  reduced-motion support on anything animated.

## Manual checklist

Run before each release, and note the date and any findings in the release notes:

- [ ] Read a `TenantGuardModelException`, a `TenantIsolationViolationException` and a
      failing probe report with a screen reader (VoiceOver, NVDA or Orca) in a test runner's
      output, and confirm each finding's rule or check and outcome is spoken
- [ ] Read the analyzer diagnostics in an editor's problems list with a screen reader
- [ ] Use the docs site with the keyboard only, at 200% zoom, and in both themes
- [ ] Check that every new message reads as a sentence on its own

## Reporting a problem

Open an issue: a bug report for output that's wrong or unreadable, or a feature request
for a change to output or docs. Or email <hello@jimmykurian.me>.

[wcag2ict]: https://www.w3.org/TR/wcag2ict-22/
[wcag22]: https://www.w3.org/TR/WCAG22/
