# Simulator Workflow

Two configuration paths converge at SimulationParameters; the
engine runs identically in both.


START
│
├── Choose data source
│ │
│ ├── Fit from data ──┐
│ │ ├── Upload CSV/XLSX │
│ │ ├── Validate │
│ │ ├── Fit distributions │
│ │ ├── Chi-square (input)│
│ │ └── λ, μ, p_exit │
│ │ │
│ └── Enter manually ───┤
│ ├── Parameter mode │
│ │ (rate / mean) │
│ ├── Time unit │
│ │ (min / sec / hr) │
│ ├── λ, per-stage μ │
│ ├── Per-stage servers │
│ └── p_exit │
│ │
│ ▼
│ SimulationParameters
│ │
│ ▼
│ Stability check — ρ per stage
│ │
│ ├── ρ < 1 for every stage → continue
│ └── any ρ ≥ 1 → STOP + explain
│
└── RUN DES
│
├── Future Event List
├── Arrivals (from distribution)
├── Queues (FIFO per stage)
├── Service (per server, from distribution)
├── Routing (p_exit after Screening)
├── Server assignment (random idle)
└── Departures
│
▼
RESULTS PANEL
├── Overview
├── Stage performance
├── Server performance
├── Statistical validation (input chi-square)
├── Simulation verification (output chi-square)
├── Analytical validation (M/M/c, guarded to
│ steady-state horizon)
├── Charts
└── Event trace
