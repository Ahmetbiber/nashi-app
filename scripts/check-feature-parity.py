#!/usr/bin/env python3
"""Fail if the Windows embedded catalog differs from the native Swift contract."""
import json, pathlib, re
root = pathlib.Path(__file__).resolve().parents[1]
source = (root / 'Sources/NashiCore/FeatureCatalog.swift').read_text()
expected = {}
for name in ['conversionMatrix', 'singleTools', 'multipleTools']:
    block = source.split('let '+name+':',1)[1].split('= [',1)[1].split('\n    ]',1)[0]
    expected[name] = {key:(re.findall(r'"([^"]+)"',value) if name=='conversionMatrix' else re.findall(r'\.([A-Za-z]+)',value)) for key,value in re.findall(r'"([a-z0-9]+)": \[([^\]]*)\]',block)}
block = source.split('let toolNames:',1)[1].split('= [',1)[1].split('\n    ]',1)[0]
expected['toolNames'] = dict(re.findall(r'\.([A-Za-z]+): "([^"]+)"',block))
actual = json.loads((root/'Resources/FeatureCatalog.json').read_text())
for name, value in expected.items():
    if actual.get(name) != value:
        raise SystemExit(f'{name}: Windows and macOS catalogs diverged. Update the shared contract and corresponding engines/tests together.')
print(f'PASS matching catalogs: {len(actual["conversionMatrix"])} input formats, {len(actual["toolNames"])} tools')
