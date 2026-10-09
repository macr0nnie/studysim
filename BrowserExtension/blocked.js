const site = new URLSearchParams(location.search).get("site");
if (site) document.getElementById("site").textContent = site;
