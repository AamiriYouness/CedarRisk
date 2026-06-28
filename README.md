# CedarRisk
 
![CI](https://github.com/AamiriYouness/CedarRisk/actions/workflows/ci.yml/badge.svg)

 
**Production-grade insurance premium rating engine for the Moroccan/French auto insurance market.**
 
Open-source portfolio project demonstrating Clean Architecture, DDD, CQRS, and insurance domain knowledge in .NET 10.
 
---
 
## What It Does
 
CedarRisk calculates the full TTC premium for a Moroccan auto insurance policy:
 
- **RC** (Responsabilité Civile) — mandatory, based on puissance fiscale × usage × CRM × prorata
- **GC** (Garanties Complémentaires) — optional, per-guarantee, five tariff modes
- **Remorque** — up to 2 trailers, proportional to RC or flat amount
- **Full fiscal chain** — CatNat, TCA (14%), Parafiscale, Timbre CNPAC per track
- **Eligibility engine** — AND/OR dependencies, bidirectional incompatibilities, age and usage rules
The engine is **stateless** — it receives a `QuoteContexte`, calculates, and returns a `QuoteResult`. No quote is persisted. The caller owns persistence.
 
---
 
## Architecture
 
```
CedarRisk/
├── src/
│   ├── CedarRisk.Domain/           # Zero external dependencies
│   │   ├── Common/                 # Result<T> monad, DomainError hierarchy, Prime tax algebra
│   │   ├── GarantieDefinitions/    # GarantieDefinition aggregate
│   │   ├── GarantieConditions/     # Eligibility rules — AND/OR/NONE value objects
│   │   ├── GarantieTarifications/  # Tariff ledger — IModeTarifaire, versioned ValidFrom/ValidTo
│   │   └── ReferentielTarifaires/  # RC barème, fiscal rates, TarifRemorque
│   │
│   ├── CedarRisk.Application/      # Use cases, engines, CQRS handlers
│   │   ├── Common/                 # IRequestHandler<TQuery, TResponse>
│   │   ├── Engines/
│   │   │   ├── Interfaces/         # Engine contracts + context/result records
│   │   │   └── Implementation/     # EligibilityEngine, RcPremiumEngine, GarantiePremiumEngine,
│   │   │                           # RemorquePremiumEngine, PremiumAggregator, QuoteEngine
│   │   └── Quotes/                 # CalculerQuoteQuery, CalculerQuoteRequestHandler, QuoteResponse
│   │
│   ├── CedarRisk.Infrastructure/   # EF Core 10, Npgsql, configurations, seed
│   │   └── Persistence/
│   │       ├── Configurations/     # Entity configurations — JSONB for ModeTarifaire, TarifRemorque
│   │       ├── Migrations/
│   │       └── Seed/               # ReferentielTarifaireSeed, GarantiesSeed (idempotent)
│   │
│   └── CedarRisk.Api/              # Minimal API, FluentValidation boundary, Scalar UI
│       └── Endpoints/              # POST /quotes/calculate
│
└── tests/
    └── CedarRisk.Tests/
        ├── Unit/
        │   ├── Domain/             # Value objects, fiscal chain, all ModeTarifaire variants
        │   ├── Engines/            # RcPremiumEngine, RemorquePremiumEngine,
        │   │                       # PremiumAggregator, QuoteEngine + FsCheck properties
        │   └── Persistence/        # TarifRemorqueJsonConverter roundtrip tests
        └── Integration/            # POST /quotes/calculate — TestContainers + real PostgreSQL
```
 
### Key Architecture Decisions
 
- **No MediatR** — `IRequestHandler<TQuery, TResponse>` interface, Scrutor for engine scanning, explicit handler registration
- **No repository layer** — handlers inject `CedarRiskDbContext` directly
- **No FluentValidation in domain** — structural validation at API boundary only; domain invariants live in factory methods and value objects
- **`Result<T>` monad** — typed error hierarchy (`NotFoundError`, `InvalidError`, `UnprocessableError`, ...), never exceptions for business rules
- **Single `SaveChangesAsync` per handler**
- **`PremiumRoundingPolicy`** — single rounding authority, never scattered
---
 
## Premium Tracks
 
RC and GC are completely independent. They only meet at `PremiumAggregator`.
 
```
RC Track                              GC Track (per guarantee)
──────────────────────────────────    ──────────────────────────────────────
Barème(PuissanceFiscale, Usage)       ValeurVenale × Taux          [TauxDirectValeurVenale]
         ↓                            OR CapitalGarantie × Taux    [TauxDirectCapitalGarantie]
   × CRM coefficient                  OR MontantFixe               [MontantFlat]
         ↓                            OR OptionChoisie.MontantHT   [CapitalOptionnel]
   × ProrataFactor                    OR CapitalClient × Taux      [TauxSurCapitalPlafonne]
         ↓                                     ↓
   × TauxCatNatRC (3.5%)              × ProrataFactor
     → CatNatRC_HT                             ↓
         ↓                            × TauxCatNatGC (per guarantee)
   TaxeRC      = PrimeRC  × 14%         → CatNatGC_HT
   CatNatTaxeRC = CatNatRC × 14%               ↓
   ParafiscaleRC = (PrimeRC + CatNatRC) × 1%  TaxeGC       = PrimeGC  × 14%
   Timbre CNPAC (fixe, RC only)        CatNatTaxeGC  = CatNatGC × 14%
         ↓                             ParafiscaleGC = (ΣPrimeGC + ΣCatNatGC) × 1%
   PrimeTTCRC                                  ↓
         └──────────────────────────  PrimeTTCGC
                          ↓
             + PrimeTTCRemorque (if NbrRemorque > 0)
                          ↓
              Math.Ceiling(PrimeTotal_TTC)
                  via PremiumRoundingPolicy
```
 
**GC is never a percentage of RC.**
 
---
 
## Rounding Rules
 
Enforced exclusively via `PremiumRoundingPolicy` — never scattered in engine code:
 
| Scope | Rule |
|-------|------|
| All intermediates | `Math.Round(x, 2, MidpointRounding.AwayFromZero)` |
| `PrimeTotal_TTC` only | `Math.Ceiling` |
| `MidpointRounding.ToEven` | Never |
 
---
 
## Tariff Modes — `IModeTarifaire`
 
Five modes covering all Moroccan auto insurance tariff structures:
 
| Mode | Formula | Typical use |
|------|---------|-------------|
| `TauxDirectValeurVenale` | `ValeurVenale × Taux` | VOL, DOM — rate on market value |
| `TauxDirectCapitalGarantie` | `CapitalGarantie × Taux` | DC — rate on a fixed guarantee capital defined at tariff level, client does not enter it |
| `MontantFlat` | fixed amount regardless of vehicle | BRIS, DF — flat premium |
| `CapitalOptionnel` | `OptionChoisie.MontantHT` from predefined tiers | PJ — client picks a coverage tier |
| `TauxSurCapitalPlafonne` | `CapitalClient × Taux` with `RegleCapital` ceiling | RC_CONDUCTEUR, PT — client declares a capital subject to ceiling rules |
 
`TauxDirectCapitalGarantie` differs from `TauxDirectValeurVenale` in that the capital base is set by the insurer at tariff time, not derived from the vehicle's current market value. This covers guarantees with a fixed sum insured (e.g. Dommages Collision on a fixed 75,000 MAD capital).
 
`RegleCapital` on `TauxSurCapitalPlafonne` supports multi-condition AND rules — e.g. `10% ≤ CapitalClient ≤ 50% of ValeurVenale` for PT.
 
---
 
## Seed Data — 2026-T1
 
**Fiscal rates (Article 284 CGI Maroc):**
 
| Rate | RC | GC |
|------|----|----|
| CatNat | 3.5% of PrimeHT → CatNatHT | Per guarantee (1.2% for VOL/DOM/DC) |
| TCA on prime | 14% | 14% |
| TCA on CatNat | 14% | 14% |
| Parafiscale | 1% of (PrimeHT + CatNatHT) | 1% of (ΣPrimeHT + ΣCatNatHT) |
| Timbre CNPAC | 10 MAD fixed | — |
| Remorque | 20% of PrimeRC | — |
 
**Garanties complémentaires:**
 
| Code | Libellé | Mode | CatNat | Conditions |
|------|---------|------|--------|------------|
| VOL | Vol & Incendie | TauxDirectValeurVenale(3%) | ✅ 1.2% | Age ≤ 10 ans |
| DOM | Dommages Collision | TauxDirectValeurVenale(2.5%) | ✅ 1.2% | Age ≤ 8 ans |
| DC | Dommages Collision Capital | TauxDirectCapitalGarantie(2%, 75 000 MAD) | ✅ 1.2% | Age ≤ 10 ans |
| BRIS | Brise-Glace | MontantFlat(400 MAD) | ❌ | AND: [VOL, DOM] |
| DF | Défense & Recours | MontantFlat(250 MAD) | ❌ | Incompatible: [PJ] |
| PJ | Protection Juridique | CapitalOptionnel(50k→800, 100k→1400, 150k→1900) | ❌ | Incompatible: [DF] |
| RC_CONDUCTEUR | RC Conducteur | TauxSurCapitalPlafonne(2%, ≤100% VV) | ✅ 1.2% | Exclus: Taxi, TPV |
| PT | Personnes Transportées | TauxSurCapitalPlafonne(1.5%, 10–50% VV) | ✅ 1.2% | OR: [VOL, DOM] + Exclus: Taxi |
 
---
 
## Running Locally
 
**Prerequisites:** .NET 10 SDK, Docker
 
```bash
# Start PostgreSQL
docker run -d \
  --name cedarrisk-dev \
  -e POSTGRES_DB=cedarrisk \
  -e POSTGRES_USER=cedar \
  -e POSTGRES_PASSWORD=cedar \
  -p 5432:5432 \
  postgres:16-alpine
 
# Apply migrations + seed (automatic on startup in Development)
dotnet run --project src/CedarRisk.Api
 
# API + interactive docs
# http://localhost:5000/scalar/v1
```
 
---
 
## API
 
### `POST /quotes/calculate`
 
Calculates the full TTC premium. No persistence — pure rating.
 
**Request:**
 
```json
{
  "puissanceFiscale": 8,
  "usage": "VehiculeTourisme",
  "valeurVenale": 120000,
  "ageVehicule": 3,
  "dateEffet": "2026-02-01",
  "dateEcheance": "2027-02-01",
  "crmCoefficient": 1.00,
  "nbrRemorque": 0,
  "garantiesGC": [
    {
      "garantieCode": "VOL",
      "capitalClient": null,
      "capitalGarantieReference": null,
      "optionChoisie": null
    },
    {
      "garantieCode": "DC",
      "capitalClient": null,
      "capitalGarantieReference": null,
      "optionChoisie": null
    },
    {
      "garantieCode": "RC_CONDUCTEUR",
      "capitalClient": 80000,
      "capitalGarantieReference": null,
      "optionChoisie": null
    }
  ]
}
```
 
**Response:**
 
```json
{
  "breakdown": {
    "primeRCHT": 950.00,
    "catNatRCHT": 33.25,
    "taxeRC": 133.00,
    "catNatTaxeRC": 4.66,
    "parafiscaleRC": 9.83,
    "timbreCNPAC": 10.00,
    "primeTTCRC": 1140.74,
    "primesGC": [
      { "garantieCode": "VOL",          "primeHT": 3600.00, "catNatHT": 43.20 },
      { "garantieCode": "DC",           "primeHT": 1500.00, "catNatHT": 18.00 },
      { "garantieCode": "RC_CONDUCTEUR","primeHT": 1600.00, "catNatHT": 19.20 }
    ],
    "totalPrimeGCHT": 6700.00,
    "totalCatNatGCHT": 80.40,
    "totalTaxeGC": 938.00,
    "totalCatNatTaxeGC": 11.26,
    "parafiscaleGC": 67.80,
    "primeTTCGC": 7797.46,
    "primeRemorqueHT": null,
    "catNatRemorqueHT": null,
    "parafiscaleRemorque": 0.00,
    "primeTTCRemorque": 0.00,
    "primeTotalTTC": 8939.00
  },
  "eligibilite": {
    "codesEligibles": ["VOL", "DC", "RC_CONDUCTEUR"],
    "ineligibles": []
  }
}
```
 
**Error responses:**
 
| Status | Condition |
|--------|-----------|
| 400 | Structural validation failure (invalid usage, CRM out of range, etc.) |
| 404 | No active `ReferentielTarifaire` for the given `dateEffet` |
| 422 | Business rule violation (capital exceeds ValeurVenale, missing barème line, etc.) |
| 503 | Transient infrastructure failure — `Retry-After` header included |
 
---
 
## Tests
 
```bash
# Unit tests only (fast, no Docker)
dotnet test CedarRisk.sln --filter "Category!=Integration"
 
# Integration tests (requires Docker — TestContainers spins up PostgreSQL)
dotnet test CedarRisk.sln --filter "Category=Integration"
 
# All tests
dotnet test CedarRisk.sln
```
 
Test results are published as a GitHub Actions check on every push to `main`. Navigate to **Actions → any run → Test Results** to see per-test pass/fail/duration.
 
**Coverage:**
 
| Layer | Type | Tool |
|-------|------|------|
| Domain value objects | Unit | xUnit + FsCheck |
| All 5 `IModeTarifaire` variants | Unit | xUnit |
| `RcPremiumEngine` — all barème tranches | Unit + Property | xUnit + FsCheck |
| `RemorquePremiumEngine` — Taux + MontantFlat | Unit + Property | xUnit + FsCheck |
| `PremiumAggregator` — fiscal chain, Math.Ceiling | Unit + Property | xUnit + FsCheck |
| `QuoteEngine` — pipeline orchestration, all failure paths | Unit | xUnit + NSubstitute |
| `TarifRemorqueJsonConverter` — JSONB roundtrip | Unit | xUnit |
| `POST /quotes/calculate` — real DB, real seed | Integration | TestContainers |
 
**FsCheck properties enforced:**
- `ProrataFactor` always in `(0, 1]`
- `CrmCoefficient` always in `[0.50, 3.50]`
- Tax always positive on positive premium
- Two remorques = exactly double one remorque (Taux mode)
- `PrimeTotalTTC` always ≥ `PrimeTTCRC`
- `Math.Ceiling` — final total is always an integer
---
 
## Tech Stack
 
| Concern | Choice |
|---------|--------|
| Runtime | .NET 10 / C# 14 |
| API | Minimal API + `Microsoft.AspNetCore.OpenApi` + Scalar UI |
| ORM | EF Core 10 + Npgsql + PostgreSQL |
| DI scanning | Scrutor |
| Validation | FluentValidation (API boundary only) |
| Testing | xUnit + FsCheck + Shouldly + NSubstitute + TestContainers |
| CI | GitHub Actions + dorny/tests-reporter |