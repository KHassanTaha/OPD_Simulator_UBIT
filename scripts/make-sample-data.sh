#!/usr/bin/env bash
# Generates the committed sample data + the dirty validation fixture.
# Usage: bash scripts/make-sample-data.sh
# Outputs (deterministic, seed 42):
#   samples/sample_patients.xlsx
#   samples/sample_patients.csv
#   tests/OpdSimulator.Data.Tests/Fixtures/dirty_missing.xlsx
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$ROOT/scripts/sample-data-generator" -- "$ROOT"