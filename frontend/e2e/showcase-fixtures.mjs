// Public documentation fixtures. All people, purchases, titles and routes are fictional.
export const models = [{
  id: 1, key: 'local-assistant', name: 'Home assistant', modelName: 'local-model',
  provider: 'Local endpoint', baseUrl: 'http://localhost:11434/v1/',
  apiKeyName: 'LOCAL_MODEL_API_KEY', paramsJson: '{"temperature":0.3}',
  isEnabled: true, isDefault: true
}];

const titles = [
  ['Beyond the Blue', 'Adventure', '#113d66', '#71d7e7'],
  ['The Last Outpost', 'Adventure', '#302842', '#c4a6e7'],
  ['Northbound', 'Adventure', '#153f35', '#a9c89d'],
  ['Golden Hour', 'Adventure', '#583020', '#eaba75'],
  ['Open Water', 'Adventure', '#193853', '#84c3d8'],
  ['The Long Way Home', 'Adventure', '#332e4c', '#f8c3ae'],
  ['Orbit Nine', 'Science Fiction', '#122a41', '#3ce5db'],
  ['After the Signal', 'Science Fiction', '#2a194b', '#e393ed'],
  ['Solar Drift', 'Science Fiction', '#493024', '#f4b259'],
  ['First Light', 'Science Fiction', '#123a44', '#91ece0'],
  ['Quiet Stars', 'Science Fiction', '#192845', '#bcc8f2'],
  ['Tomorrow Again', 'Science Fiction', '#4b2342', '#e798b2']
];
export const movies = titles.map(([title, genre, background, accent], i) => ({
  id: i + 1, title, genre, year: String(2022 + i % 4),
  description: 'An original demo story about curiosity, unexpected discoveries, and finding your way home. Fictional media for the HomeApp showcase.',
  filePath: `/mnt/movies/Demo/${title}.mp4`, thumbnail: `/showcase/poster-${i}.svg`,
  watched: i === 2, background, accent
}));
export const movieGroups = Object.groupBy(movies, movie => movie.genre);
export const series = [
  { id: 101, title: 'Harbor Days', genre: 'Drama', seasonCount: 3, episodeCount: 24, watchedEpisodeCount: 12, fullyWatched: false, thumbnail: '/showcase/poster-4.svg', description: 'Life, friendship, and new beginnings in a fictional coastal town.' },
  { id: 102, title: 'The Observatory', genre: 'Science Fiction', seasonCount: 2, episodeCount: 16, watchedEpisodeCount: 5, fullyWatched: false, thumbnail: '/showcase/poster-6.svg', description: 'A small team follows a signal beyond the edge of the known sky.' },
  { id: 103, title: 'Weekend Kitchen', genre: 'Lifestyle', seasonCount: 2, episodeCount: 20, watchedEpisodeCount: 20, fullyWatched: true, thumbnail: '/showcase/poster-3.svg', description: 'Simple recipes, shared tables, and a little weekend inspiration.' }
];
export const episodes = Array.from({ length: 8 }, (_, i) => ({
  id: 201 + i, title: ['A New Shore', 'The Invitation', 'Low Tide', 'Good Neighbors', 'The Crossing', 'A Change of Wind', 'Home Again', 'Lights on the Water'][i],
  season: 1, episodeNumber: i + 1, watched: i < 4,
  filePath: `/mnt/tv/Demo/Harbor Days/S01E0${i + 1}.mp4`, thumbnail: '/showcase/poster-4.svg',
  description: 'An episode from the fictional HomeApp demo library.'
}));

export function posterSvg(movie, i) {
  const stars = Array.from({ length: 35 }, (_, n) => `<circle cx="${(n * 73 + i * 29) % 400}" cy="${(n * 47) % 330}" r="${n % 3 === 0 ? 1.5 : .7}" fill="white" opacity=".4"/>`).join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="600" viewBox="0 0 400 600">
    <defs><linearGradient id="bg" x2=".7" y2="1"><stop stop-color="${movie.background}"/><stop offset="1" stop-color="#090f1d"/></linearGradient><radialGradient id="glow"><stop stop-color="${movie.accent}" stop-opacity=".45"/><stop offset="1" stop-color="${movie.accent}" stop-opacity="0"/></radialGradient></defs>
    <rect width="400" height="600" fill="url(#bg)"/>${stars}<circle cx="200" cy="230" r="180" fill="url(#glow)"/>
    <circle cx="${130 + i % 3 * 55}" cy="${190 + i % 4 * 20}" r="${56 + i % 3 * 18}" fill="${movie.accent}" opacity=".85"/>
    <path d="M0 355L90 ${230 + i % 3 * 30}L165 340L275 ${190 + i % 4 * 35}L400 355V600H0Z" fill="#111e2b"/>
    <path d="M0 400L105 330L190 375L310 285L400 380V600H0Z" fill="${movie.background}"/>
    <path d="M0 447Q180 370 400 440V600H0Z" fill="#09101c"/>
    <text x="200" y="60" fill="${movie.accent}" text-anchor="middle" font-family="sans-serif" font-size="11" letter-spacing="5">HOMEAPP DEMO COLLECTION</text>
    <text x="200" y="566" fill="${movie.accent}" text-anchor="middle" font-family="sans-serif" font-size="12" letter-spacing="4">${movie.genre.toUpperCase()}</text></svg>`;
}

export const groups = [
  { id: 1, key: 'groceries', name: 'Groceries', color: '#22c55e', displayOrder: 0, isEnabled: true },
  { id: 2, key: 'dining', name: 'Dining', color: '#f59e0b', displayOrder: 1, isEnabled: true },
  { id: 3, key: 'transport', name: 'Transport', color: '#38bdf8', displayOrder: 2, isEnabled: true },
  { id: 4, key: 'home', name: 'Home', color: '#a78bfa', displayOrder: 3, isEnabled: true }
];
const purchases = [
  [1, 1, 'Maple Market', 48.75], [2, 2, 'Corner Cafe', 12.50], [3, 3, 'City Transit', 28],
  [4, 1, 'Green Basket', 62.40], [5, 4, 'Oak & Linen', 89.95], [6, 2, 'Harbor Kitchen', 36.80],
  [7, 1, 'Maple Market', 42.15], [8, 3, 'City Transit', 28], [9, 2, 'Corner Cafe', 9.60],
  [10, 1, 'Green Basket', 57.90], [11, 4, 'Oak & Linen', 34.50], [12, 2, 'Harbor Kitchen', 44.20],
  [13, 1, 'Maple Market', 73.60], [14, 3, 'City Transit', 28], [15, 2, 'Corner Cafe', 14.25],
  [16, 1, 'Green Basket', 51.20], [18, 4, 'Oak & Linen', 119], [20, 2, 'Harbor Kitchen', 39.50],
  [22, 1, 'Maple Market', 66.30], [24, 3, 'City Transit', 28], [25, 1, 'Green Basket', 58.45],
  [27, 2, 'Corner Cafe', 18.75], [29, 4, 'Oak & Linen', 42], [30, 1, 'Maple Market', 71.10]
];
export const expenses = purchases.map(([day, groupId, merchantName, totalAmount], i) => ({
  id: i + 1, merchantName, expenseGroupId: groupId, expenseGroupName: groups[groupId - 1].name,
  expenseGroupColor: groups[groupId - 1].color, sourceType: i % 5 === 0 ? 'csv-import' : 'receipt',
  expenseDate: `2026-09-${String(day).padStart(2, '0')}`, totalAmount, currencyCode: 'CAD',
  description: ['Weekly essentials', 'Coffee and lunch', 'Transit pass', 'Household supplies'][groupId - 1],
  isWorkExpense: false, isReimbursable: false, analysisConfidence: .96,
  createdAtUtc: '2026-09-30T16:00:00Z', updatedAtUtc: '2026-09-30T16:00:00Z'
}));
export const dashboard = {
  expenses, groups, settings: { modelKey: 'local-assistant', defaultCurrencyCode: 'CAD', extractionPrompt: '' },
  models: [{ key: 'local-assistant', name: 'Home assistant', modelId: 'local-model', providerName: 'Local endpoint', isDefault: true }],
  summary: {
    totalAmount: expenses.reduce((sum, expense) => sum + expense.totalAmount, 0), expenseCount: expenses.length,
    averageAmount: expenses.reduce((sum, expense) => sum + expense.totalAmount, 0) / expenses.length,
    latestExpenseDate: '2026-09-30',
    totalsByGroup: groups.map(group => ({ expenseGroupId: group.id, name: group.name, color: group.color,
      totalAmount: expenses.filter(e => e.expenseGroupId === group.id).reduce((sum, e) => sum + e.totalAmount, 0),
      expenseCount: expenses.filter(e => e.expenseGroupId === group.id).length }))
  }
};
export const receiptPreview = {
  reviewToken: 'documentation-only', expenseGroupId: 1, expenseDate: '2026-09-30', totalAmount: 48.75,
  subtotalAmount: 46.43, taxAmount: 2.32, currencyCode: 'CAD', merchantName: 'Maple Market',
  location: 'Demo town', paymentMethod: 'Card', modelKeyUsed: 'local-assistant',
  receiptText: 'MAPLE MARKET — fictional sample receipt',
  extraction: { confidence: .96, items: [
    { name: 'Fresh apples', quantity: 1, unit: 'bag', unitPrice: 5.49, totalPrice: 5.49 },
    { name: 'Oat milk', quantity: 2, unit: 'carton', unitPrice: 4.29, totalPrice: 8.58 },
    { name: 'Sourdough bread', quantity: 1, unit: 'loaf', unitPrice: 6.49, totalPrice: 6.49 },
    { name: 'Pantry essentials', quantity: 1, totalPrice: 25.87 }
  ] }
};
export const itemComparisons = [
  ['Oat milk', 'oat milk', [4.29, 3.79, 4.49], ['Maple Market', 'Green Basket', 'Maple Market']],
  ['Sourdough bread', 'sourdough bread', [6.49, 5.99], ['Maple Market', 'Green Basket']],
  ['Fresh apples', 'fresh apples', [5.49, 4.99], ['Maple Market', 'Green Basket']]
].map(([name, normalizedName, prices, merchants], i) => ({
  name, normalizedName, purchaseCount: prices.length, lowestPrice: Math.min(...prices),
  lowestPriceMerchant: merchants[prices.indexOf(Math.min(...prices))],
  purchases: prices.map((price, j) => ({ id: i * 10 + j, expenseId: j + 1, name,
    canonicalName: normalizedName, quantity: 1, unit: 'each', unitPrice: price,
    totalPrice: price, comparablePrice: price, currencyCode: 'CAD', merchantName: merchants[j],
    location: 'Demo town', expenseDate: `2026-09-${20 + j * 3}` }))
}));

export const chatSessions = [
  { id: 1, title: 'A weekend worth slowing down for', modelKey: 'local-assistant', createdAtUtc: '2026-09-30T14:00:00Z', updatedAtUtc: '2026-09-30T14:05:00Z' },
  { id: 2, title: 'Ideas for the reading corner', modelKey: 'local-assistant', createdAtUtc: '2026-09-29T14:00:00Z', updatedAtUtc: '2026-09-29T14:05:00Z' },
  { id: 3, title: 'A simple weeknight menu', modelKey: 'local-assistant', createdAtUtc: '2026-09-28T14:00:00Z', updatedAtUtc: '2026-09-28T14:05:00Z' }
];
export const chatDetail = { ...chatSessions[0], messages: [
  { id: 1, role: 'user', content: 'Help me plan a relaxed weekend at home: a movie night, a few easy meals, and some time outdoors.', createdAtUtc: '2026-09-30T14:00:00Z' },
  { id: 2, role: 'assistant', modelKey: 'local-assistant', modelName: 'Home assistant', providerName: 'Local endpoint', createdAtUtc: '2026-09-30T14:01:00Z',
    content: '## Make room for the good stuff\n\nKeep it simple: one good meal, one good story, and a little fresh air.\n\n| When | The plan |\n| --- | --- |\n| Friday evening | Homemade pizza and a movie from your library |\n| Saturday morning | Coffee, a short walk, and a slow breakfast |\n| Saturday evening | Roast vegetables and a board game |\n| Sunday | Batch a soup for the week, then unwind |\n\n**Movie-night idea:** choose an adventure, dim the lights, and put the phones away.\n\nWant a shopping list for the meals?' }
] };

const routeId = '11111111-1111-1111-1111-111111111111';
const profileId = '22222222-2222-2222-2222-222222222222';
export const demoRoute = {
  id: routeId, name: 'Riverside → Harbor district', origin: 'Riverside', destination: 'Harbor district',
  direction: 'Eastbound', roadways: ['Demo Parkway'], alertRegions: [], corridorKm: 1,
  points: [{ latitude: .004, longitude: -.012 }, { latitude: .003, longitude: -.003 }, { latitude: .006, longitude: .006 }, { latitude: .011, longitude: .012 }],
  marineBounds: { minLatitude: -.01, maxLatitude: .02, minLongitude: -.02, maxLongitude: .02 },
  bridge: { name: 'Harbor lift bridge', position: { latitude: .006, longitude: .006 }, inboundBearing: 225 }
};
export const profile = { id: profileId, name: 'Morning overview', routeIds: [routeId], favoriteViews: [{ cameraId: 'camera-1', viewId: 'view-1' }], panels: ['cameras', 'bridge', 'reports', 'marine', 'map'], favoritesOnly: false };
export const workspace = { routes: [demoRoute], dashboards: [profile], activeRouteId: routeId, activeDashboardId: profileId, googleMapsConfigured: false };
export const cameras = ['Parkway at Riverside', 'Harbor bridge approach'].map((name, i) => ({
  id: `camera-${i + 1}`, name, roadway: 'Demo Parkway', direction: 'Eastbound',
  position: { latitude: .004 + i * .003, longitude: -.006 + i * .012 },
  views: [{ id: `view-${i + 1}`, description: 'Eastbound overview', enabled: true, url: `/showcase/camera-${i}.svg` }]
}));
export function commuteSnapshot() {
  const now = new Date();
  const iso = now.toISOString();
  return {
    route: demoRoute, dashboard: profile, settingsAvailable: true, status: 'CAUTION',
    summary: 'Lane works on the parkway. A possible bridge lift needs attention.', cameras,
    items: [
      { id: 'works', kind: 'construction', title: 'Parkway lane improvements', description: 'One lane reduced near the harbor approach. Allow extra time.', roadway: 'Demo Parkway', direction: 'Eastbound', position: { latitude: .0045, longitude: .002 }, geometry: [], updatedAt: iso, startsAt: null, endsAt: null, fullClosure: false, lanes: 'Right lane', schedule: 'Weekdays, 09:00–15:00', regional: false, advisory: true },
      { id: 'condition', kind: 'condition', title: 'Road surface: bare and dry', description: 'Demo conditions along the monitored corridor.', roadway: 'Demo Parkway', direction: 'Both directions', position: { latitude: .009, longitude: .010 }, geometry: [], updatedAt: iso, startsAt: null, endsAt: null, fullClosure: false, lanes: '', schedule: '', regional: false, advisory: false }
    ],
    vessels: [{ mmsi: '123456789', name: 'MV Northstar (demo)', position: { latitude: .012, longitude: .006 }, course: 225, heading: 225, speedKnots: 6.8, typeCode: 70, type: 'Cargo', lengthMetres: 145, beamMetres: 22, destination: 'DEMO PORT', seenAt: iso, source: 'Demo AIS', navigationStatus: 0, positionFreshnessKnown: true }],
    predictions: [{ level: 'POSSIBLE', vesselMmsi: '123456789', vesselName: 'MV Northstar (demo)', direction: 'Inbound', windowStart: new Date(+now + 8 * 60000).toISOString(), windowEnd: new Date(+now + 18 * 60000).toISOString(), confidence: 55, explanation: 'Demo approach aligned with the crossing. Advisory estimate; not an opening schedule.' }],
    feeds: ['cameras', 'incidents', 'construction', 'conditions', 'alerts', 'marine'].map((id, i) => ({ id, name: ['Traffic cameras', 'Incidents', 'Roadworks', 'Road conditions', 'Regional alerts', 'Marine AIS'][i], state: 'ready', lastSuccess: iso, lastAttempt: iso, nextAttempt: null, message: 'Demo feed · sample data' })),
    bridge: demoRoute.bridge.position, generatedAt: iso, attribution: ['Illustrated demo map and camera views. Fictional routes and provider data.']
  };
}
export function cameraSvg(i) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540" viewBox="0 0 960 540">
    <defs><linearGradient id="sky" x2="0" y2="1"><stop stop-color="#94b6c8"/><stop offset="1" stop-color="#e3edf0"/></linearGradient></defs>
    <rect width="960" height="540" fill="url(#sky)"/><path d="M0 240Q180 170 340 220T960 210V540H0Z" fill="#657d6c"/>
    <path d="M0 300L410 230H550L960 330V540H0Z" fill="#91a584"/><path d="M170 540L450 230H510L810 540Z" fill="#46555f"/>
    <path d="M480 540V460M480 420V370M480 342V310M480 290V270" stroke="#f2ecd1" stroke-width="7"/>
    <path d="M220 540L454 240M760 540L506 240" stroke="white" stroke-width="4" opacity=".8"/>
    ${i ? '<path d="M235 305H715M285 240V415M665 240V415M285 255L665 330M665 255L285 330" stroke="#506879" stroke-width="12" fill="none"/>' : ''}
    <rect x="360" y="390" width="65" height="35" rx="5" fill="#ced8df"/><rect x="530" y="315" width="36" height="24" rx="3" fill="#263c53"/>
    <rect x="20" y="20" width="305" height="38" rx="4" fill="#10283b" opacity=".85"/><text x="35" y="46" fill="white" font-family="sans-serif" font-size="18">ILLUSTRATED DEMO · EASTBOUND</text></svg>`;
}
export const mapTile = `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256"><rect width="256" height="256" fill="#e9ede5"/><g fill="#d5e2cf"><rect x="12" y="12" width="80" height="67" rx="5"/><rect x="144" y="145" width="99" height="98" rx="6"/></g><g fill="#ddd9d1"><rect x="145" y="12" width="43" height="42"/><rect x="201" y="12" width="42" height="42"/><rect x="12" y="148" width="38" height="37"/><rect x="63" y="148" width="35" height="37"/><rect x="12" y="199" width="86" height="42"/></g><path d="M0 114H256M117 0V256M136 66H256M57 136V256" stroke="white" stroke-width="11"/><path d="M0 114H256M117 0V256" stroke="#f4d291" stroke-width="4"/><text x="155" y="96" fill="#869487" font-size="10" font-family="sans-serif">DEMO DISTRICT</text></svg>`;

export const receiptSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="480" height="640"><rect width="480" height="640" fill="#e9e3d7"/><rect x="44" y="20" width="392" height="600" rx="4" fill="#fffdf7"/><g fill="#2f3533" font-family="monospace"><text x="240" y="76" text-anchor="middle" font-size="28" font-weight="bold">MAPLE MARKET</text><text x="240" y="104" text-anchor="middle" font-size="13">FICTIONAL SAMPLE RECEIPT</text><text x="72" y="156" font-size="16">30 SEP 2026             10:42</text><path d="M72 178H408M72 360H408M72 468H408" stroke="#a0a39b" stroke-dasharray="4 4"/><g font-size="17"><text x="72" y="218">Fresh apples</text><text x="408" y="218" text-anchor="end">5.49</text><text x="72" y="256">Oat milk x2</text><text x="408" y="256" text-anchor="end">8.58</text><text x="72" y="294">Sourdough bread</text><text x="408" y="294" text-anchor="end">6.49</text><text x="72" y="332">Pantry essentials</text><text x="408" y="332" text-anchor="end">25.87</text><text x="72" y="395">SUBTOTAL</text><text x="408" y="395" text-anchor="end">46.43</text><text x="72" y="434">TAX</text><text x="408" y="434" text-anchor="end">2.32</text><text x="72" y="510" font-size="24" font-weight="bold">TOTAL CAD</text><text x="408" y="510" text-anchor="end" font-size="24" font-weight="bold">48.75</text></g><text x="240" y="565" text-anchor="middle" font-size="15">Thank you for shopping local.</text></g></svg>`;
