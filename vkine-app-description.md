# Vkine — App Description

## What is Vkine?

Vkine is a cinema discovery app for the Czech market. It lets users explore what movies are currently playing in Czech cinemas, find showtimes filtered by date and time of day, and discover upcoming premieres. It aggregates showtimes from Czech cinemas and enriches each movie with ratings from ČSFD (Czech film database), TMDb, and IMDb.

---

## Pages

The app has two pages, accessible via a pill-shaped segmented navigation control in the top-right corner of the toolbar.

### 1. Movies

The main page. Displays all movies currently showing in Czech cinemas as a browsable grid. Contains all filtering and search controls.

### 2. Premieres

Displays upcoming movie premieres grouped by month (e.g. March 2026, April 2026…). Shows the total count of upcoming premieres. Uses the same movie card design as the Movies page. No search or filter controls — just a chronological timeline of what's coming to cinemas.

---

## Movie Card

The basic unit of the UI. Each card contains:

- **Poster image** — fills the top portion of the card. If no poster is available, a gradient background with the movie's initials is shown instead.
- **Title** — bold, truncated if too long.
- **Year and country** — secondary gray text below the title.
- **Rating badges** — compact colored pills for each available rating source:
  - **ČSFD** — red/orange, shows percentage (e.g. "89%")
  - **TMDb** — teal/green, shows score (e.g. "7.4")
  - **IMDb** — yellow, shows score (e.g. "8.1")
  - Only sources with available data are shown.

Clicking a card opens the movie detail.

---

## Movie Detail

On desktop, the detail opens as a modal overlay without navigating away. On mobile, it navigates to a full-page detail view.

### Hero Section
- Full-width widescreen backdrop image from the film with a dark gradient fade at the bottom.
- Over the backdrop (bottom-left aligned):
  - Movie title in large bold text
  - Original title in italics (if different)
  - Year, duration, country of origin, original language
  - Rating badges (same ČSFD / TMDb / IMDb pills, slightly larger than on cards)
- A close button (×) is in the top-right corner.
- A "Skip to showtimes ↓" link allows jumping past the metadata.

### Body Section
- **Synopsis** — full description paragraph.
- **Crew** — horizontally scrollable row of pill chips, each showing a small avatar (photo or initial), the person's name, and their role (Director, Producer, etc.). Each chip links to a Google search for that person.
- **Top 10 Cast** — a horizontal row of portrait thumbnails with the actor's name and character name below each.
- **YouTube trailer** — embedded player if a trailer is available.
- **Showtimes** — grouped first by date, then by cinema venue (see Showtimes section below).
- A scroll-to-top button (↑) floats within the modal for easy navigation.

---

## Showtimes

Within the movie detail, showtimes are presented as:

- **Date tabs** — a calendar strip showing available dates (labeled "Today", "Tomorrow", or the weekday name).
- **By venue** — under each date, showtimes are grouped by cinema. The cinema name is shown with inline links to find its location on Apple Maps and Google Maps.
- **Each showtime** shows:
  - Start time (e.g. 18:30)
  - Special format badges — e.g. "CZ-SUB" (Czech subtitles), "CZ-DUB" (Czech dubbing), "Dolby Atmos", "4DX"
  - Ticket availability status
  - A link to purchase tickets (opens in a new tab)

If active date or time filters are applied, a notice is shown in the detail explaining that the showtimes are filtered.

---

## Filtering & Search (Movies Page Only)

All controls are always visible in the sticky toolbar — no hidden panel or "Filters" button. Filters are combinable and their state is saved in the URL so links can be shared and the browser back button works correctly.

### Search
- A wide text input spanning most of the toolbar width.
- Placeholder: "Search movies, descriptions, cast and crew"
- Results appear automatically after a short typing pause.
- Pressing Enter triggers a broader search.
- Clear the search with an × button inside the input.
- The count of results is shown above the grid (e.g. "12 movies").

### Date Range
- A pill-shaped input with a calendar icon.
- Clicking it opens a calendar to select a start and end date.
- Only movies with at least one showtime in the selected date range are shown.
- An active filter chip appears below with a clear (×) action.

### Time of Day
- A horizontal slider ranging from 9:00 AM to 11:30 PM.
- Dragging it filters movies to only show those with showtimes starting at or after the selected time.
- An active filter chip appears with a clear (×) action.

### Sort
- Three pill buttons: **Rating**, **Name**, **Year**.
- Clicking the active button toggles the sort direction (ascending ↑ / descending ↓).
- Defaults: Rating descending, Name A→Z, Year descending.
- The active sort with its direction is shown as a filter chip that can also be cycled or cleared.

---

## Toolbar Layout

The toolbar is sticky — it stays at the top of the screen while scrolling.

**Row 1:**
- Search input (left, takes most of the width)
- Segmented navigation pill: **Movies** | **Premieres** (right)
- Language toggle button — shows current language code (e.g. "EN"). Clicking cycles through available languages and reloads the page.
- Theme toggle — sun icon (light mode) / moon icon (dark mode).

**Row 2 (Movies page only):**
- Date range picker
- Time slider
- Sort buttons (Rating / Name / Year)

---

## Light & Dark Mode

The app fully supports both light and dark themes. The toggle is always visible in the toolbar. The selected theme persists across sessions.

| Element | Light | Dark |
|---|---|---|
| Page background | Light gray | Near-black |
| Cards | White | Very dark |
| Text | Near-black | Off-white |
| Accent / interactive | Vivid blue | Brighter blue |
| Toolbar | Frosted glass (light tint) | Frosted glass (dark tint) |

Rating badge brand colors (ČSFD red, TMDb teal, IMDb yellow) remain the same in both modes.

---

## Language Support

The app supports Czech and English. The language toggle in the toolbar cycles between them. Language affects:
- Movie titles (Czech title vs. English title)
- Synopses (Czech vs. English version)
- UI labels

---

## Responsive Behavior

- **Desktop:** 6 movie cards per row. Movie detail opens as a modal overlay.
- **Mobile:** 2 movie cards per row. Movie detail navigates to a full-page view. Toolbar compresses, with the search input taking full width. The modal supports swipe-down to close.

---

## Visual Design Language

The app follows an Apple-inspired design system (similar to iOS/macOS aesthetics):

- System fonts throughout
- Rounded corners on all surfaces (cards, inputs, buttons, modals)
- Subtle shadows on cards and modals
- Muted, slightly transparent backgrounds for the toolbar (frosted glass effect)
- Compact, pill-shaped controls for navigation, filters, and rating badges
- Blue as the primary accent/interactive color
- Gray secondary text for metadata
- Generous but compact spacing — the grid is dense but not cluttered
- No loud decorative elements — the movie posters and backdrop images provide all the visual richness

---

## User Flows

---

## REST API

All endpoints are read-only. There is no authentication. The primary identifier for movies throughout the API is the ČSFD (Česká filmová databáze) numeric ID.

---

### GET /api/movies

Returns a paginated list of movies that have at least one upcoming showtime, ordered by earliest showtime.

**Query parameters:**
- `skip` — how many movies to skip (default: 0)
- `take` — how many movies to return (default: 20)

**Each movie contains:**
- `id` — ČSFD numeric ID
- `tmdbId`, `imdbId` — IDs on TMDb and IMDb (nullable)
- `title` — Czech title
- `titleEn` — English title
- `originalTitle` — original language title
- `synopsis` — short synopsis
- `descriptionCs`, `descriptionEn` — full descriptions in Czech and English
- `csfdRating` — ČSFD rating as a percentage string (nullable)
- `tmdbRating` — TMDb numeric rating (nullable)
- `imdbRating`, `imdbRatingCount` — IMDb rating and vote count (nullable)
- `coverUrl` — poster image URL
- `backdropUrl` — widescreen backdrop image URL
- `year` — release year or year range
- `duration` — formatted duration (e.g. "1:33")
- `genres` — list of genre strings
- `originCountryCodes`, `originCountries` — country codes and full names
- `originalLanguage` — original spoken language
- `cast`, `crew`, `directors` — lists of names
- `homepage` — official website URL
- `trailerUrl` — YouTube trailer URL (nullable)
- `credits` — structured list of credited people, each with:
  - `name` — person's name
  - `role` — their role (Director, Producer, Actor, etc.)
  - `photoUrl` — portrait photo URL (nullable)
  - `tmdbId` — their TMDb person ID (nullable)

---

### GET /api/movies/search

Searches movies by free text. Only returns movies with upcoming showtimes. Returns at most 50 results.

**Query parameters:**
- `q` (required) — search query. Returns 400 if missing.

**Search behavior:**
- Searches across: titles (Czech, English, original), descriptions (Czech and English), cast, crew, and director names.
- All space-separated words in the query must match — AND semantics.
- Case-insensitive and accent-insensitive (e.g. "svihaci" matches "Šviháci").

**Response shape:** same as `/api/movies`.

---

### GET /api/movies/{csfdId}/showtimes

Returns all upcoming showtimes for a single movie across all venues, grouped by date and venue.

**URL parameter:**
- `csfdId` — the movie's ČSFD ID

**Response:** a list of schedule entries, one per date:
- `date` — date in `YYYY-MM-DD` format
- `movieId` — ČSFD movie ID
- `performances` — list of per-venue entries:
  - `venueId` — numeric venue ID
  - `showtimes` — list of individual screenings:
    - `startAt` — start time in `HH:mm` format
    - `ticketsAvailable` — whether tickets can be purchased
    - `ticketUrl` — link to purchase tickets (nullable)
    - `isPast` — whether the screening has already started
    - `badges` — list of special format/language indicators, each with:
      - `code` — short label (e.g. `CZ-SUB`, `CZ-DUB`, `IMAX`, `4DX`, `DOLBY`)
      - `description` — human-readable description (nullable)
      - `kind` — category: `Technology`, `Format`, or `Language`

**Filtering applied server-side:**
- Only dates from today onwards are returned.
- Within today, showtimes that have already passed are excluded.
- Venues with no remaining showtimes are omitted.

---

### GET /api/premieres

Returns all upcoming movie premieres, ordered by premiere date ascending. No parameters.

**Each premiere contains:**
- `csfdId` — ČSFD movie ID (use this to fetch full movie data from `/api/movies/search` or cross-reference with the movies list)
- `premiereDate` — object with `year`, `month`, `day` fields

Only premieres with a date of today or later are returned.

---

### GET /proxy/tmdb/{path}

Image proxy for TMDb images. Forwards requests to TMDb's CDN and returns the image with a 30-day cache header and cross-origin access enabled. Used by the client for poster and backdrop images.

- `path` — the TMDb image path (e.g. `t/p/w500/abc123.jpg`)

---

### Find a movie to watch tonight
1. Open the Movies page.
2. Set the date filter to today and the time slider to the current hour.
3. Browse (or search) the filtered results.
4. Click a movie → see showtimes → click a showtime → buy tickets.

### Discover something good
1. Open the Movies page with Rating sort (default).
2. Scroll through the top-rated movies currently in cinemas.
3. Click any card to read the synopsis, see the cast, and watch the trailer.

### Plan ahead
1. Use the date range picker to select a future weekend.
2. Browse what will be showing.
3. Pick a movie and note the available showtimes.

### Explore what's coming soon
1. Navigate to Premieres.
2. Scroll through upcoming releases month by month.
3. Click any title to see its details.
