# CedarRisk

![CI](https://github.com/AamiriYouness/CedarRisk/actions/workflows/ci.yml/badge.svg)

**Production-grade insurance premium rating engine for the Moroccan/French auto insurance market.**

Open-source portfolio project demonstrating Clean Architecture, DDD, CQRS, and insurance domain knowledge in .NET 10.

---

## What It Does

CedarRisk calculates the full TTC premium for a Moroccan auto insurance policy:

- **RC** (Responsabilité Civile) — mandatory, based on puissance fiscale × usage × CRM × prorata
- **GC** (Garanties Complémentaires) — optional, per-guarantee, four tariff modes
- **Remorque** — up to 2 trailers, proportional to RC or flat amount
- **Full fiscal chain** — CatNat, TCA, Parafiscale, Timbre CNPAC per track
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
        │   ├── Domain/             # Value objects, fiscal chain, ModeTarifaire variants
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
   TaxeRC     = PrimeRC  × 14%          → CatNatGC_HT
   CatNatTaxeRC= CatNatRC × 14%                ↓
   ParafiscaleRC= (PrimeRC + CatNatRC) × 1%  TaxeGC      = PrimeGC  × 14%
   Timbre CNPAC (fixe, RC only)        CatNatTaxeGC = CatNatGC × 14%
         ↓                             ParafiscaleGC= (ΣPrimeGC + ΣCatNatGC) × 1%
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

| Mode | Formula | Used by |
|------|---------|---------|
| `TauxDirectValeurVenale` | `ValeurVenale × Taux` | VOL, DOM |
| `MontantFlat` | fixed amount | BRIS, DF |
| `CapitalOptionnel` | `OptionChoisie.MontantHT` (fixed tiers) | PJ |
| `TauxSurCapitalPlafonne` | `CapitalClient × Taux` with `RegleCapital` ceiling | RC_CONDUCTEUR, PT |

`RegleCapital` supports multi-condition AND rules (e.g. `10% ≤ capital ≤ 50% ValeurVenale`).

---

## Seed Data — 2026-T1

**Fiscal rates:**

| Rate | RC | GC |
|------|----|----|
| CatNat | 1.2% | 1.2% |
| TCA | 14% | 14% |
| TaxeSurCatNat | 3.5% | 14% |
| Parafiscale | 1% | 1% |
| Timbre CNPAC | 10 MAD | — |
| Remorque | 20% of PrimeRC | — |

**Garanties complémentaires:**

| Code | Libellé | Mode | CatNat | Conditions |
|------|---------|------|--------|------------|
| VOL | Vol & Incendie | TauxDirectValeurVenale(3%) | ✅ | Age ≤ 10 ans |
| DOM | Dommages Collision | TauxDirectValeurVenale(2.5%) | ✅ | Age ≤ 8 ans |
| BRIS | Brise-Glace | MontantFlat(400) | ❌ | AND: [VOL, DOM] |
| DF | Défense & Recours | MontantFlat(250) | ❌ | Incompatible: [PJ] |
| PJ | Protection Juridique | CapitalOptionnel(50k→800, 100k→1400, 150k→1900) | ❌ | Incompatible: [DF] |
| RC_CONDUCTEUR | RC Conducteur | TauxSurCapitalPlafonne(2%, ≤100% VV) | ✅ | Exclus: Taxi, TPV |
| PT | Personnes Transportées | TauxSurCapitalPlafonne(1.5%, 10–50% VV) | ✅ | OR: [VOL, DOM] + Exclus: Taxi |

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
  postgres:18-alpine3.23

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
    "catNatRCHT": 11.40,
    "taxeRC": 133.00,
    "catNatTaxeRC": 0.40,
    "parafiscaleRC": 9.61,
    "timbreCNPAC": 10.00,
    "primeTTCRC": 1114.41,
    "primesGC": [
      { "garantieCode": "VOL", "primeHT": 3600.00, "catNatHT": 43.20 }
    ],
    "totalPrimeGCHT": 3600.00,
    "totalCatNatGCHT": 43.20,
    "totalTaxeGC": 504.00,
    "totalCatNatTaxeGC": 6.05,
    "parafiscaleGC": 36.43,
    "primeTTCGC": 4189.68,
    "primeRemorqueHT": null,
    "catNatRemorqueHT": null,
    "parafiscaleRemorque": 0.00,
    "primeTTCRemorque": 0.00,
    "primeTotalTTC": 5305.00
  },
  "eligibilite": {
    "codesEligibles": ["VOL", "RC_CONDUCTEUR"],
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

**Coverage:**

| Layer | Type | Tool |
|-------|------|------|
| Domain value objects | Unit | xUnit + FsCheck |
| All 4 `IModeTarifaire` variants | Unit | xUnit |
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
| CI | GitHub Actions |
| Document generation | QuestPDF (Conditions Particulières — planned) |


