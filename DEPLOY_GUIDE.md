sudo chmod 755 /www/server/panel/data/compose/hcs/certs
sudo chmod 644 /www/server/panel/data/compose/hcs/certs/openiddict.crt
sudo chmod 644 /www/server/panel/data/compose/hcs/certs/openiddict.key
sudo chmod 644 /www/server/panel/data/compose/hcs/certs/openiddict.pfx




deploy create cert

cd /www/server/panel/data/compose/hcs/certs

openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
-keyout openiddict.key \
-out openiddict.crt \
-subj "/CN=auth-dev.benhvien199.vn"

openssl pkcs12 -export \
-out openiddict.pfx \
-inkey openiddict.key \
-in openiddict.crt \
-passout pass:StrongPass123



docker cp /www/server/panel/data/compose/hcs/certs/openiddict.pfx hc-api:/app/certs/openiddict.pfx
chown 1654:1654 /www/server/panel/data/compose/hcs/certs/openiddict.pfx
chmod 600 /www/server/panel/data/compose/hcs/certs/openiddict.pfx

chown -R 1654:1654 /www/server/panel/data/compose/hcs/certs
chmod 700 /www/server/panel/data/compose/hcs/certs
