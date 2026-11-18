# Decisions
- Folder is C:\Projects\HireLens; user created it.
- .NET 10.0.400 pinned in global.json; Angular CLI 22.1.7; SQL Server LocalDB.
- Angular app in /web, API in /src.
- Identity user (AppUser) lives in Infrastructure to keep Domain dependency-free.
- Packages pinned to 10.* for EF/Identity/JWT (latest NuGet targets net11 previews).
