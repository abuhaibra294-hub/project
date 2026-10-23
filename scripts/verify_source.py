"""Lightweight offline structural validation; NOT a C# compiler replacement."""
from __future__ import annotations
from pathlib import Path
import json, re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
for json_file in root.rglob('*.json'):
    json.loads(json_file.read_text(encoding='utf-8'))
for xml_file in [*root.rglob('*.csproj'), root / 'DareCare.slnx']:
    ET.parse(xml_file)

# Ignores string and comment contents, so curly braces in XML/JSON/email text aren't treated as C# syntax.
def mask_csharp(src: str) -> str:
    out = list(src)
    i = 0
    state = 'code'
    while i < len(src):
        c = src[i]
        n = src[i + 1] if i + 1 < len(src) else ''
        if state == 'code':
            if c == '/' and n == '/': state, i = 'line-comment', i + 2; continue
            if c == '/' and n == '*': state, i = 'block-comment', i + 2; continue
            if c == '"': state, i = 'string', i + 1; continue
            if c == "'": state, i = 'char', i + 1; continue
        elif state == 'line-comment':
            if c == '\n': state = 'code'
            else: out[i] = ' '
            i += 1; continue
        elif state == 'block-comment':
            out[i] = ' '
            if c == '*' and n == '/': out[i + 1] = ' '; state, i = 'code', i + 2; continue
            i += 1; continue
        elif state in {'string','char'}:
            out[i] = ' '
            if c == '\\':
                if i + 1 < len(src): out[i + 1] = ' '
                i += 2; continue
            if (state == 'string' and c == '"') or (state == 'char' and c == "'"):
                state = 'code'
            i += 1; continue
        i += 1
    return ''.join(out)

errors = []
cs_files = list(root.rglob('*.cs'))
for file in cs_files:
    src = mask_csharp(file.read_text(encoding='utf-8'))
    nesting = []
    pairs = {')': '(', ']': '[', '}': '{'}
    for c in src:
        if c in '({[': nesting.append(c)
        if c in ')}]':
            if not nesting or nesting.pop() != pairs[c]: errors.append(f'{file}: mismatched {c}')
    if nesting: errors.append(f'{file}: unmatched {nesting}')
if errors: raise SystemExit('\n'.join(errors))
assert (root / 'src/DareCare.Api/wwwroot/auth.html').exists()
assert 'RefreshSession' in (root / 'src/DareCare.Infrastructure/Persistence/DareCareDbContext.cs').read_text()
assert 'OnTokenValidated' in (root / 'src/DareCare.Api/Program.cs').read_text()
assert 'RotateAsync' in (root / 'src/DareCare.Api/Security/JwtTokenService.cs').read_text()
assert 'darecare-sprint5-dev.db' in (root / 'src/DareCare.Api/Program.cs').read_text()
for required in ['START_DARECARE.cmd', 'START_SQLSERVER.cmd', 'TEST_DARECARE.cmd',
                 'STOP_DARECARE.cmd', 'SMOKE_DARECARE.cmd',
                 'scripts/Start-DareCare.ps1', 'scripts/Smoke-Workflow.ps1',
                 'src/DareCare.Api/Controllers/PrescriptionsController.cs',
                 'src/DareCare.Api/Controllers/PharmacyOrdersController.cs',
                 'src/DareCare.Api/Controllers/MatchingController.cs',
                 'docs/PHARMACY_FLOW.md',
                 'docs/SPRINT5_API_CONTRACTS.md', 'docs/SPRINT5_QA.md',
                 'SHOW_APIS.cmd', 'src/DareCare.Api/Controllers/SupportController.cs',
                 'src/DareCare.Api/Controllers/PatientAddressesController.cs',
                 'src/DareCare.Api/Controllers/FavoriteNursesController.cs',
                 'tests/DareCare.Api.Tests/Sprint5OwnershipTests.cs',
                 'tests/DareCare.Api.Tests/Sprint5PersistenceTests.cs']:
    assert (root / required).is_file(), f'Missing: {required}'

print(f'PASS: JSON / project XML / {len(cs_files)} C# files have balanced structures and expected assets.')
print('NOTE: .NET 10 SDK unavailable here; build, unit tests, PowerShell launcher and database migrations need runtime verification on Windows.')
