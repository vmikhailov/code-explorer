# CodeExplorer Website (`code-explorer.mikhailov.cc`)

Source code and assets for the official CodeExplorer landing page, interactive playground, and documentation portal: [https://code-explorer.mikhailov.cc](https://code-explorer.mikhailov.cc).

## Directory Structure

```
site/
├── index.html              # Single-page interactive landing, playground, and documentation
├── favicon.svg             # Favicon asset
├── logo.svg                # Vector brand logo
├── all_services.png        # Retina showcase: Macro architecture & orbits
├── selected_services.png   # Retina showcase: Sub-graph neighborhood & custom orbits
├── project_flow.png        # Retina showcase: Project flow, service cards & C1 context
├── CNAME                   # Custom domain record (code-explorer.mikhailov.cc)
├── robots.txt              # Public crawler policy and sitemap location
├── sitemap.xml             # Sitemap for search crawlers
├── deploy.sh               # One-click deployment script to production VPS
└── README.md               # Documentation and operations manual (this file)
```

## Local Development & Preview

The site is a standalone single-page application built with modern vanilla HTML, CSS, and JavaScript with zero external build-time dependencies.

Preview locally using any static web server:

```bash
# Python 3
cd site && python3 -m http.server 8080

# Or Node.js
npx serve site
```

Visit: `http://localhost:8080`

## Deployment

The website is hosted on Nginx on the DigitalOcean droplet (`calendars.cc` / `146.190.24.85`) under `/var/www/code-explorer` with SSL via Let's Encrypt.

To deploy changes to production:

```bash
cd site
./deploy.sh
```
