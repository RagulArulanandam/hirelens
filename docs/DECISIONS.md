# Decisions
- Folder is C:\Projects\HireLens; user created it.
- .NET 10.0.400 pinned in global.json; Angular CLI 22.1.7; SQL Server LocalDB.
- Angular app in /web, API in /src.
- Identity user (AppUser) lives in Infrastructure to keep Domain dependency-free.
- Packages pinned to 10.* for EF/Identity/JWT (latest NuGet targets net11 previews).
- Admin password and JWT key generated randomly into user-secrets (read with: dotnet user-secrets list --project src/HireLens.Api).
- Tests swap SQL Server for EF InMemory via a WebApplicationFactory override.
- PDF extraction uses the official 'PdfPig' package (UglyToad.PdfPig id only has a custom prerelease on NuGet).
- ATS score = keywords 60 + sections 15 + contact 10 + dates 5 + format 10; keywords = curated skill vocabulary + repeated JD terms.
