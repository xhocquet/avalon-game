import{j as e}from"./index-BS5PrCYD.js";import{m as g,t as j}from"./gdd-meta-Dd20hGQR.js";import{u as s}from"./vendor-DLwYKAIE.js";function r(i){const n={a:"a",blockquote:"blockquote",code:"code",h2:"h2",li:"li",ol:"ol",p:"p",strong:"strong",ul:"ul",...s(),...i.components},{Figure:t}=n;return t||c("Figure"),e.jsxs(e.Fragment,{children:[e.jsx(n.h2,{children:"Links"}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.wolfire.com/blog/2011/03/GDC-Session-Summary-Halo-networking/",children:"GDC Session Summary: Halo Networking"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://gdcvault.com/play/1014346/I-Shot-You-First-Networking",children:"I Shot You First: Networking in Halo (GDC Vault)"})})}),`
`,e.jsxs("div",{className:"link-item link-item--complete",children:[e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.forrestthewoods.com/blog/tech_of_planetary_annihilation_chrono_cam/#foot05",children:"Tech of Planetary Annihilation: ChronoCam"})}),e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:["Two broad server/networking models:",`
`,e.jsxs(n.ol,{children:[`
`,e.jsx(n.li,{children:"Sync/lockstep: all clients run a perfectly synchronized simulation; usually peer-to-peer, but can also have a server"}),`
`,e.jsx(n.li,{children:"Client-server: all clients connect to a server that maintains the authoritative game state, and clients receive updates that represent that state"}),`
`]}),`
`]}),`
`,e.jsx(n.li,{children:"Another big decision: use curves to represent and update data"}),`
`,e.jsxs(n.li,{children:["The article describes adding points of ",e.jsx(n.code,{children:"(time, value)"})," for many attributes such as position, health, and vision"]}),`
`,e.jsx(n.li,{children:"This does not seem radically different from the AoE-style idea, more like a different semantic abstraction for how state over time gets represented"}),`
`,e.jsx(n.li,{children:"ChronoCam is then just traversal of those curves, with interpolation between saved points"}),`
`,e.jsx(n.li,{children:"One neat property of curves: the scale can vary wildly across cases; if something needs to travel a huge distance, you may still only need to store two points"}),`
`,e.jsx(n.li,{children:"You can also predict the future by following a curve"}),`
`,e.jsxs(n.li,{children:["Useful for:",`
`,e.jsxs(n.ol,{children:[`
`,e.jsx(n.li,{children:"Completing progress once you already have enough points"}),`
`,e.jsx(n.li,{children:"Traveling/rendering toward a known future point"}),`
`]}),`
`]}),`
`,e.jsx(n.li,{children:"You can always alter curves later by sending corrections, though that is more expensive"}),`
`,e.jsx(n.li,{children:"Classic tick problem: if an overlap/collision/event happens between ticks, graphics and/or calculations can get messed up"}),`
`,e.jsx(n.li,{children:"This is similar to the issue described in the Rocket League notes: lower-resolution discrete steps can miss or distort interactions"}),`
`,e.jsx(n.li,{children:"Curves get around this by letting you interpolate between points at whatever resolution you need"}),`
`,e.jsx(n.li,{children:'You can implement "curves" however you want; the abstraction is flexible'}),`
`,e.jsx(n.li,{children:"Step curves: useful for things like ammo, where the value just drops immediately with no interpolation"}),`
`,e.jsx(n.li,{children:"Pulse curves/events: just a time and a point, with no need for interpolation; useful for simple event streams like sounds or UI"}),`
`]})]}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://planetaryannihilation.com/support/server-performance/",children:"Planetary Annihilation Server Performance"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://randomascii.wordpress.com/2013/07/16/floating-point-determinism/",children:"Floating Point Determinism (Random ASCII)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.forrestthewoods.com/blog/synchronous_rts_engines_and_a_tale_of_desyncs/",children:"Synchronous RTS Engines and a Tale of Desyncs"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://web.archive.org/web/20210204100736/https://docs.unrealengine.com/udk/Three/NetworkingOverview.html#Implement%20Networking%20from%20the%20beginning!",children:"UDK Networking Overview (Wayback Machine)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.raphkoster.com/games/presentations/from-1-to-n-multiplayer-game-design/",children:"From 1 to N: Multiplayer Game Design (Raph Koster)"})})}),`
`,e.jsxs("div",{className:"link-item link-item--complete",children:[e.jsx(n.p,{children:e.jsx(n.a,{href:"https://zoo.cs.yale.edu/classes/cs538/readings/papers/terrano_1500arch.pdf",children:"1500 Archers on a 28.8: Age of Empires Networking (Terrano & Bettner)"})}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Requirements: 8 players, large battles, smooth over LAN/internet"}),`
`,e.jsx(n.li,{children:"Target platform: 16MB Pentium 90 with a 28.8 modem"}),`
`,e.jsx(n.li,{children:"Performance target: 15 FPS"}),`
`,e.jsx(n.li,{children:"Passing x/y coordinates directly was wildly inefficient; they estimated that approach would only support about 250 units"}),`
`,e.jsx(n.li,{children:"This is the birth of the synced-simulation idea: run the same game on every player's PC and send only commands"}),`
`,e.jsx(n.li,{children:"The minimum network data is the input/command stream, not continuous x/y updates for every unit"}),`
`,e.jsx(n.li,{children:"They tagged commands for frames in the future"}),`
`,e.jsx(n.li,{children:"Result: each client could receive commands for the current frame, future frames, and even farther-ahead frames while the engine kept running regardless"}),`
`,e.jsxs(n.li,{children:["Core turn-processing loop:",`
`,e.jsxs(n.ol,{children:[`
`,e.jsx(n.li,{children:"Accept player commands for the current command turn"}),`
`,e.jsx(n.li,{children:"Wait until the turn-time window elapses, while monitoring game state and ping"}),`
`,e.jsx(n.li,{children:'Send a "done" message with timing/count data and increment the command turn'}),`
`,e.jsx(n.li,{children:`Wait until every player's "done" message arrives, while also checking for drops/timeouts`}),`
`,e.jsx(n.li,{children:"Advance the shared turn, adjust timing for the next turn, then run the next game/render step"}),`
`]}),`
`]}),`
`]}),e.jsxs(n.blockquote,{children:[`
`,e.jsx(n.p,{children:'"Turns were typically 200 msec in length, with commands being sent out during the turn. After 200 msec, the turn was cut off and the next turn was started. At any point during the game, commands were being processed for one turn, received and stored for the next turn, and sent out for execution two turns in the future."'}),`
`]}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:'"Speed control": they adjusted turn length based on the makeup of the group'}),`
`,e.jsx(n.li,{children:"This looks like a P2P/lockstep artifact; with a server model this specific mechanism would not apply the same way"}),`
`,e.jsx(n.li,{children:"They used UDP"}),`
`,e.jsx(n.li,{children:"Clients handled command ordering, drop detection, and resending themselves"}),`
`,e.jsx(n.li,{children:"Turn numbers were included with commands/messages"}),`
`]}),e.jsx(n.p,{children:e.jsx(n.strong,{children:"Lessons learned"})}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Know your user: understand their expectations and develop against them"}),`
`,e.jsx(n.li,{children:"Meter data/network use early and throttle aggressively enough to create a stable foundation during development"}),`
`,e.jsx(n.li,{children:"Make the network/tooling output human-readable so it is practical to inspect and debug"}),`
`,e.jsx(n.li,{children:"Build sample apps to test third-party tech directly so you can verify what it can actually do"}),`
`]}),e.jsx(n.p,{children:e.jsx(n.strong,{children:"RTS3 network model"})}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Level 1: sockets"}),`
`,e.jsx(n.li,{children:"Use the socket API provided by the language/platform of choice"}),`
`]}),e.jsx(t,{src:"/docs/assets/misc/socket-api-diagram.webp"}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Level 2: link level"}),`
`,e.jsx(n.li,{children:"Transport-layer services: slightly higher-level abstractions the game code can use to establish connections and destinations for players"}),`
`,e.jsxs(n.li,{children:["Includes ",e.jsx(n.code,{children:"Link"}),", ",e.jsx(n.code,{children:"Listener"}),", ",e.jsx(n.code,{children:"NetworkAddress"}),", ",e.jsx(n.code,{children:"Packet"}),", and ",e.jsx(n.code,{children:"Ping"})]}),`
`,e.jsx(n.li,{children:"Level 3: multiplayer"}),`
`,e.jsx(n.li,{children:"These are the actual high-level abstractions used by the game"}),`
`,e.jsxs(n.li,{children:[e.jsx(n.code,{children:"Client"}),": representation of a player"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.code,{children:"Session"}),": main container for multiplayer objects; contains clients, manages connections, and owns the overall multiplayer state"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.code,{children:"Chan"})," and ",e.jsx(n.code,{children:"OrderedChannel"}),": pipes used to send data; ordered channels add delivery/ordering behavior"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.code,{children:"SharedData"}),": extend this object to define methods for syncing/updating across multiplayer"]}),`
`,e.jsxs(n.li,{children:[e.jsx(n.code,{children:"TimeSync"}),": manages synchronization of global game time across clients"]}),`
`,e.jsx(n.li,{children:"Level 4: game comms"}),`
`,e.jsx(n.li,{children:"Actual game-specific helpers used for debugging, testing, development, and production gameplay"}),`
`]})]}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.gamedevs.org/uploads/introduction-to-sync-host.pdf",children:"Introduction to Sync Host (PDF)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.gamedevs.org/uploads/networking-for-physics-programmers.pdf",children:"Networking for Physics Programmers (PDF)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.gamedevs.org/uploads/tribes-networking-model.pdf",children:"The Tribes Networking Model (Frohnmayer & Gift)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking",children:"Source Multiplayer Networking (Valve Developer Community)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://web.archive.org/web/20150214035339/http://forum.valhallalegends.com/index.php/topic,17702.0.html",children:"Valhalla Legends: RTS Networking Discussion (Wayback Machine)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html",children:"Client-Side Prediction and Server Reconciliation (Gabriel Gambetta)"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://gafferongames.com/",children:"Gaffer On Games"})})}),`
`,e.jsx("div",{className:"link-item",children:e.jsx(n.p,{children:e.jsx(n.a,{href:"https://gafferongames.com/categories/game-physics/",children:"Game Physics (Gaffer On Games)"})})}),`
`,e.jsxs("div",{className:"link-item link-item--complete",children:[e.jsx(n.p,{children:e.jsx(n.a,{href:"https://www.gamedevs.org/uploads/It-IS-Rocket-Science-The-Physics-of-Rocket-League-Detailed.mp4",children:"It IS Rocket Science: The Physics of Rocket League Detailed (GDC)"})}),e.jsxs(n.ul,{children:[`
`,e.jsxs(n.li,{children:["Uses ",e.jsx(n.a,{href:"https://github.com/bulletphysics/bullet3",children:"Bullet Physics"})," (open source — debuggable and modifiable), single-threaded"]}),`
`,e.jsx(n.li,{children:"Discrete collision detection (actor moves, then finds collisions) vs. continuous collision detection — chosen for efficiency"}),`
`,e.jsx(n.li,{children:"Fixed tick rate = deterministic physics; they use 120hz / 8.33ms"}),`
`,e.jsx(n.li,{children:"120hz gave much more consistency than 60hz — at lower tick rates the timestep is large enough that discrete collision detection becomes inconsistent (objects can pass through or miss each other)"}),`
`,e.jsx(n.li,{children:"Slower rate → larger steps → larger penetrations → inconsistent hits; higher rate → consistency"}),`
`,e.jsx(n.li,{children:"High tick rate is expensive, especially for network corrections — presenter says in hindsight he wishes they'd found another solution rather than pushing hz so high"}),`
`]}),e.jsx(n.p,{children:e.jsx(n.strong,{children:"Vehicle Tuning"})}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Goals: fast acceleration/braking, sharp steering, stable on ground, fast recovery"}),`
`,e.jsx(n.li,{children:"Acceleration: increase torque → but torque depends on friction → friction depends on spin rate → spin rate depends on wheel radius → wheel radius affected by suspension → all influenced by gravity + mass; gears used to tune acceleration ratios"}),`
`,e.jsx(n.li,{children:"Reducing complexity: no transmission — transmission sounds are faked; use force/acceleration curves instead, easy to tweak and create distinct vehicle feels"}),`
`,e.jsx(n.li,{children:"Mass: keep constant, don't let the engine auto-calculate it; ignore mass when applying forces (decouples feel from physics weight)"}),`
`,e.jsx(n.li,{children:"Tire friction: removed entirely, not simulated"}),`
`,e.jsxs(n.li,{children:["Simple friction model: ",e.jsx(n.code,{children:"ratio = sideSpeed / (sideSpeed + forwardSpeed)"})," → ",e.jsx(n.code,{children:"slideFriction = curve(ratio)"}),"; curves hold the tuning data rather than physics constants"]}),`
`]}),e.jsx(n.p,{children:e.jsx(n.strong,{children:"Networking"})}),e.jsxs(n.ul,{children:[`
`,e.jsx(n.li,{children:"Challenges: input delay not viable for fast-paced games; client prediction needed for rigid body vehicles; server can't wait for client input; collisions with moving objects (ball); 100% server authoritative"}),`
`,e.jsx(n.li,{children:"Why can't server wait? Players don't reliably send inputs on time → server can't block on them → simulation diverges between clients and server (non-deterministic)"}),`
`,e.jsx(n.li,{children:"Hitting moving objects: client predicts its own vehicle (ahead of server); server is authoritative on the ball; client interpolates the ball — problem: client's vehicle and the ball exist in two different timelines (client-predicted vs. server-authoritative)"}),`
`,e.jsx(n.li,{children:"Lag compensation (classic): client predicts shot → sends to server → server confirms → fine for hitscan/projectiles"}),`
`,e.jsx(n.li,{children:"For RL: client predicts hitting the ball, server receives the hit — but problem: a laggy client's ball hit affects all other players, so lag compensation breaks down in shared-physics scenarios"}),`
`,e.jsx(n.li,{children:"Solution: server buffers player inputs; client predicts everything"}),`
`,e.jsx(n.li,{children:"Input buffers: client streams inputs every frame; inputs don't arrive at a constant rate, so server buffers them and takes the last available input each physics tick — no pausing for inputs, also eliminates some connection-based cheats; downside: increases average latency"}),`
`,e.jsx(n.li,{children:"Buffer goals: avoid empty buffers (stalls) and avoid large buffers (adds latency)"}),`
`,e.jsx(n.li,{children:"Upstream throttle (Overwatch's approach): server tells client to run faster or slower — if buffer is low, client runs extra frames to fill it; if full, client slows down"}),`
`,e.jsx(n.li,{children:"Downstream throttle (RL's approach): server consumes 0, 1, or 2 inputs per frame — buffer low → use 1 input for 2 frames; buffer full → consume 2 inputs for 1 frame; downside: can allow minor desyncs"}),`
`,e.jsx(n.li,{children:"Predicting everything: client predicts all other cars and the ball; when a server correction arrives, client replays frames forward from the corrected state to catch back up"}),`
`,e.jsx(n.li,{children:"Standard practice: send ~10 player inputs per packet rather than 1 at a time (redundancy covers dropped packets)"}),`
`]})]})]})}function a(i={}){const{wrapper:n}={...s(),...i.components};return n?e.jsx(n,{...i,children:e.jsx(r,{...i})}):r(i)}function c(i,n){throw new Error("Expected component `"+i+"` to be defined: you likely forgot to import, pass, or provide it.")}function l(i){const{Section:n}={...s(),...i.components};return n||h("Section"),e.jsx(n,{title:"Networking",id:"networking",children:e.jsx(a,{})})}function d(i={}){const{wrapper:n}={...s(),...i.components};return n?e.jsx(n,{...i,children:e.jsx(l,{...i})}):l(i)}function h(i,n){throw new Error("Expected component `"+i+"` to be defined: you likely forgot to import, pass, or provide it.")}function o(i){return e.jsx(d,{})}function p(i={}){const{wrapper:n}={...s(),...i.components};return n?e.jsx(n,{...i,children:e.jsx(o,{...i})}):o()}export{p as default,g as meta,j as themeColors};
