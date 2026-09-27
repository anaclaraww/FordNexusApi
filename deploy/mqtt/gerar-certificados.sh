#!/bin/sh
set -eu
cd /certs

if [ -f ca.crt ]; then
  echo "Certificados já existem em deploy/mqtt/certs. Apague a pasta para gerar de novo."
  exit 0
fi

apk add --no-cache openssl >/dev/null

openssl req -x509 -newkey rsa:3072 -sha256 -days 365 -nodes \
  -keyout ca.key -out ca.crt -subj "/O=Ford Nexus/CN=Ford Nexus IoT CA"

printf "subjectAltName=DNS:mosquitto,DNS:localhost\nextendedKeyUsage=serverAuth\nkeyUsage=digitalSignature,keyEncipherment\n" > server.ext
printf "extendedKeyUsage=clientAuth\nkeyUsage=digitalSignature,keyEncipherment\n" > client.ext

emitir() {
  nome=$1; cn=$2; ext=$3
  openssl req -newkey rsa:2048 -nodes -keyout "$nome.key" -out "$nome.csr" -subj "/O=Ford Nexus/CN=$cn"
  openssl x509 -req -in "$nome.csr" -CA ca.crt -CAkey ca.key -CAcreateserial \
    -out "$nome.crt" -days 365 -sha256 -extfile "$ext"
  rm "$nome.csr"
}

emitir server mosquitto server.ext
emitir api ford-nexus-api client.ext
emitir veiculo-ka 9BFZH55L0G8123456 client.ext
emitir veiculo-ecosport 9BFZB55P7K8765432 client.ext

rm -f server.ext client.ext ca.srl
chmod 644 ./*.crt ./*.key
chmod 600 ca.key
echo "Certificados gerados: CA, broker, API e dois veículos (Ka e EcoSport)."
