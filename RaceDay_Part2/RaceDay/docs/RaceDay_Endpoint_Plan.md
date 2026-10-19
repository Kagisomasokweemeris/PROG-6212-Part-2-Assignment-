# RaceDay - API Endpoint Plan

Authentication uses a server-side **session** (cookie `.RaceDay.Session`) that stores the user id and role.
"Any" = any logged-in user. All error bodies are RFC 7807 `ProblemDetails` JSON.
401 = not logged in, 403 = logged in with the wrong role (or not the owner of the resource).

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates an account. Organisers must supply the secret `organiserCode` so nobody can self-promote. | None (public) | `{ firstName, lastName, email, password, dateOfBirth, phoneNumber?, role, organiserCode? }` | 201 Created - profile; 400 invalid data/role; 403 bad organiser code; 409 email taken |
| POST | /api/auth/login | Verifies credentials and starts a session holding user id and role. | None (public) | `{ email, password }` | 200 OK - profile; 401 invalid credentials |
| POST | /api/auth/logout | Ends the current session. | Any | None | 204 No Content; 401 not logged in |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/profile | Returns the logged-in user's own profile. | Any | None | 200 OK - profile; 401 |
| PUT | /api/profile | Updates the logged-in user's name, date of birth and phone number. | Any | `{ firstName, lastName, dateOfBirth, phoneNumber? }` | 200 OK - updated profile; 400; 401 |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events?type= | Lists events ordered by date, optionally filtered by Run/Walk/Cycle. | Any | None | 200 OK - event list; 400 bad type; 401 |
| GET | /api/events/{id} | Returns one event. | Any | None | 200 OK - event; 404; 401 |
| POST | /api/events | Creates an event owned by the logged-in organiser. | Organiser | `{ name, description, eventDate, location, distanceKm, eventType }` | 201 Created - event; 400; 401; 403 |
| PUT | /api/events/{id} | Updates an event the organiser owns. | Organiser (owner) | `{ name, description, eventDate, location, distanceKm, eventType }` | 200 OK - event; 400; 401; 403; 404 |
| DELETE | /api/events/{id} | Deletes an event with its categories, enrolments and results. | Organiser (owner) | None | 204 No Content; 401; 403; 404 |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists the age/distance categories of an event. | Any | None | 200 OK - category list; 401; 404 |
| POST | /api/events/{eventId}/categories | Adds a category (age range and/or distance) to an owned event. | Organiser (owner) | `{ name, minAge?, maxAge?, distanceKm? }` | 201 Created - category; 400; 401; 403; 404 |
| PUT | /api/categories/{id} | Updates a category on an owned event. | Organiser (owner) | `{ name, minAge?, maxAge?, distanceKm? }` | 200 OK - category; 400; 401; 403; 404 |
| DELETE | /api/categories/{id} | Deletes a category nobody has enrolled in. | Organiser (owner) | None | 204 No Content; 401; 403; 404; 409 in use |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/enrolments | Enters the logged-in participant into an event under a chosen category (age eligibility checked on race day). | Participant | `{ categoryId }` | 201 Created - enrolment; 400 wrong category/age/past event; 401; 403; 404; 409 already enrolled |
| GET | /api/enrolments/my | Lists the participant's own enrolments. | Participant | None | 200 OK - enrolment list; 401; 403 |
| DELETE | /api/enrolments/{id} | Withdraws from an event (own enrolment, no result yet). | Participant (owner) | None | 204 No Content; 401; 403; 404; 409 result exists |
| GET | /api/events/{eventId}/enrolments | Lists every enrolment for an owned event. | Organiser (owner) | None | 200 OK - enrolment list; 401; 403; 404 |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/results | Captures finish time and position for an enrolment. | Organiser (owner) | `{ enrolmentId, finishTime (hh:mm:ss), finishPosition }` | 201 Created - result; 400; 401; 403; 404; 409 duplicate result/position |
| PUT | /api/results/{id} | Corrects a captured result. | Organiser (owner) | `{ finishTime, finishPosition }` | 200 OK - result; 400; 401; 403; 404; 409 |
| GET | /api/events/{eventId}/results | Lists all results of an owned event by position. | Organiser (owner) | None | 200 OK - result list; 401; 403; 404 |
| GET | /api/results/my | Lists the participant's own results history. | Participant | None | 200 OK - result list; 401; 403 |
