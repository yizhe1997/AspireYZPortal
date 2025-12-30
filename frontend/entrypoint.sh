#!/bin/sh
set -e

# Write runtime env JS that the SPA can read
cat > /usr/share/nginx/html/env-config.js <<EOF
window.__ENV = {
  API_URL: "${API_URL:-/api}"
}
EOF

exec nginx -g 'daemon off;'
