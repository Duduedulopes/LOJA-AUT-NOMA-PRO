/* Smart Store AI — service worker
   Estratégia: cache-first para o app shell, network-first para o resto.
   Suba a versão do CACHE ao publicar mudanças. */

const CACHE = "smart-store-v3";

const SHELL = [
  "./",
  "./index.html",
  "./manifest.webmanifest",
  "./shared/dados.js",
  "./admin/",
  "./admin/index.html",
  "./admin/manifest.webmanifest",
  "./icons/icon-192.png",
  "./icons/icon-512.png"
];

self.addEventListener("install", e=>{
  e.waitUntil(
    caches.open(CACHE)
      .then(c=>Promise.allSettled(SHELL.map(u=>c.add(u))))
      .then(()=>self.skipWaiting())
  );
});

self.addEventListener("activate", e=>{
  e.waitUntil(
    caches.keys()
      .then(ks=>Promise.all(ks.filter(k=>k!==CACHE).map(k=>caches.delete(k))))
      .then(()=>self.clients.claim())
  );
});

self.addEventListener("fetch", e=>{
  const req = e.request;
  if(req.method !== "GET") return;

  const url = new URL(req.url);

  /* chamadas de API nunca entram no cache */
  if(url.origin !== location.origin) return;

  e.respondWith(
    caches.match(req).then(hit=>{
      if(hit) return hit;
      return fetch(req).then(res=>{
        if(res && res.status === 200 && res.type === "basic"){
          const copia = res.clone();
          caches.open(CACHE).then(c=>c.put(req, copia));
        }
        return res;
      }).catch(()=>caches.match("./index.html"));
    })
  );
});
