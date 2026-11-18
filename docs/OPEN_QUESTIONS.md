# Open questions

1. **Anthropic API key**: none was available, so real Claude scoring is untested against the live API (the HTTP flow, retry and token accounting are covered by tests with a fake handler). Set it with `dotnet user-secrets set "Anthropic:ApiKey" "..."`.
2. **Model IDs**: defaults `claude-haiku-5-5` / `claude-sonnet-5-5` come from the brief; confirm they exist on your account.
3. **Scanned (image-only) PDFs** have no extractable text and are reported as failed; OCR is not included.
4. **Production hardening**: JWT in localStorage, no refresh tokens, CORS origin list is dev-only. Review before deploying.
