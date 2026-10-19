# RaceDay

Event management system for South African road running, walking and cycling events.
This repository holds **Part 1 (planning + database)** and **Part 2 (RESTful API + tests + CI)** as one continuous project.

## Repository layout

| Path | Contents |
|---|---|
| `docs/` | Part 1: `RaceDay_ERD.png`, `RaceDay_Endpoint_Plan.md`, `RaceDay_Database.sql` (+ `erd.dot` source) |
| `src/RaceDay.Api/` | Part 2: ASP.NET Core 8 Web API (EF Core code-first, session auth, Swagger) |
| `tests/RaceDay.Tests/` | xUnit tests (in-memory API + in-memory database) |
| `.github/workflows/ci.yml` | Validates `/docs`, builds, runs all tests on every push |

The API was built from the endpoint plan; every route in `docs/RaceDay_Endpoint_Plan.md` is implemented and visible in Swagger. The EF Core model matches the ERD and the SQL script table for table and column for column (the only extras are EF's `HasData` role seed and the same constraints written in SQL).

## Roles

- **Organiser** - creates, edits and deletes their own events, manages categories, views all enrolments for their events, captures and corrects results. Registering as Organiser needs the secret `OrganiserCode` (`Registration:OrganiserCode` in `appsettings.json`), so nobody can promote themselves.
- **Participant** - registers, browses events and categories, enters an event by choosing a category, views and withdraws their own enrolments, views their own results.

Access is enforced in the API by session: 401 when not logged in, 403 when the role (or ownership) is wrong.

## Setup and run

Prerequisites: .NET 8 SDK, SQL Server (LocalDB / Express / full) .

1. (Optional) Run `docs/RaceDay_Database.sql` in SSMS to create `RaceDayDb` with sample data. All seed users have the password `Password123!`.
2. Check the connection string in `src/RaceDay.Api/appsettings.json`.
3. Run:
   ```
   dotnet run --project src/RaceDay.Api
   ```
   Open `/swagger`. Log in first through `POST /api/auth/login`; the browser keeps the session cookie.
   If you skipped step 1, the API creates the schema from the EF model on startup. To use real migrations instead:
   `dotnet ef migrations add InitialCreate --project src/RaceDay.Api` (the API applies them automatically).
4. Tests: `dotnet test`

## Notes for Part 3

Session state lives in the API's server memory and a cookie. The MVC app will need to forward that cookie when it calls the API.

## Submission checklist (yours to fill in)

- [ ] YouTube video link (Part 1): _add here_
- [ ] YouTube video link (Part 2): _add here_
- [ ] Screenshot of green CI build: _add here_ (`docs/ci-green.png`)
- [ ] At least 20 meaningful commits per part from your own GitHub account
