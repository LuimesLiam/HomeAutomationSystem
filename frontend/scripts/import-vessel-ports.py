"""Generate an ignored deployment destination gazetteer from an NGA World Port Index CSV.
Usage: python frontend/scripts/import-vessel-ports.py /path/to/world_port_index.csv
No runtime geocoding or external request is needed.
"""
import csv
import json
import math
import pathlib
import re
import sys

ports = []
for row in csv.DictReader(open(sys.argv[1], encoding='utf-8-sig')):
    try:
        lat, lon = float(row['Latitude']), float(row['Longitude'])
    except ValueError:
        continue
    if not (math.isfinite(lat) and math.isfinite(lon) and abs(lat) <= 90 and abs(lon) <= 180):
        continue
    code = re.sub(r'\s', '', row['UN/LOCODE']).upper()
    if not re.fullmatch(r'[A-Z]{2}[A-Z0-9]{3}', code):
        code = ''
    name = row['Main Port Name'].strip()
    country = row['Country Code'].strip()
    alternate = row['Alternate Port Name'].strip()
    if name:
        ports.append([code, name, country, lat, lon, alternate])
ports.sort(key=lambda p: (p[0], p[1], p[3], p[4]))
target = pathlib.Path(__file__).resolve().parents[1] / 'src/assets/vessel-ports.private.json'
target.write_text(json.dumps(ports, ensure_ascii=False, separators=(',', ':')) + '\n')
print(f'Wrote {len(ports)} port locations')
