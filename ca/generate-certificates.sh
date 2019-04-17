#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

path="${DIR}/../certs"

docker network create ssl-test

rm -rf ${DIR}/../certs
mkdir -p ${DIR}/../certs

# https://pki-tutorial.readthedocs.io/en/latest/
# Generate Root CA
mkdir -p ${DIR}/../certs/root-ca/private ${DIR}/../certs/root-ca/db ${DIR}/../crl
chmod 700 ${DIR}/../certs/root-ca/private

touch ${DIR}/../certs/root-ca/db/root-ca.db
touch ${DIR}/../certs/root-ca/db/root-ca.db.attr
echo 01 > ${DIR}/../certs/root-ca/db/root-ca.crt.srl
echo 01 > ${DIR}/../certs/root-ca/db/root-ca.crl.srl

openssl req -new \
    -config ${DIR}/root-ca.conf \
    -out ${DIR}/../certs/root-ca.csr \
    -keyout ${DIR}/../certs/root-ca/private/root-ca.key

openssl ca -selfsign -batch \
    -config ${DIR}/root-ca.conf \
    -in ${DIR}/../certs/root-ca.csr \
    -out ${DIR}/../certs/root-ca.crt \
    -extensions root_ca_ext

# Generate Signing CA
mkdir -p ${DIR}/../certs/signing-ca/private ${DIR}/../certs/signing-ca/db ${DIR}/../crl
chmod 700 ${DIR}/../certs/signing-ca/private

touch ${DIR}/../certs/signing-ca/db/root-ca.db
touch ${DIR}/../certs/signing-ca/db/root-ca.db.attr
echo 01 > ${DIR}/../certs/signing-ca/db/root-ca.crt.srl
echo 01 > ${DIR}/../certs/signing-ca/db/root-ca.crl.srl

openssl req -new \
    -config ${DIR}/signing-ca.conf \
    -out ${DIR}/../certs/signing-ca.csr \
    -keyout ${DIR}/../certs/signing-ca/private/signing-ca.key

openssl ca -batch \
    -config ${DIR}/root-ca.conf \
    -in ${DIR}/../certs/signing-ca.csr \
    -out ${DIR}/../certs/signing-ca.crt \
    -extensions signing_ca_ext

docker rm -f intermediateca
docker build --rm -t cfssltest-intermediateca -f ${DIR}/../ca/CA.Dockerfile ${DIR}/..
docker run -d -p 8889:8888 --network ssl-test --name intermediateca cfssltest-intermediateca

certname='test.local'
caaddress='localhost:8889'

echo $certname
echo $caaddress
# Generate Certificate
curl -d '{ "request": {
  "CN": "$certname",
  "hosts":["'$certname'", "testapp"],
  "key": { "algo": "rsa", "size": 2048 }, 
  "names": [
    {"C":"US", "ST":"California", "L":"San Francisco", "O":"'$certname'"},
    {"C":"US", "ST":"California", "L":"San Francisco", "O":"testapp"}
   ]
  }
}' -o $path/tmpcert.json http://$caaddress/api/v1/cfssl/newcert

# Create Private Key
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep private_key | cut -f4 -d '"')" > $path/$certname.key

# Create Certificate
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep -m 1 certificate | cut -f4 -d '"')" > $path/$certname.cer

# Create Certificate Request
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep certificate_request | cut -f4 -d '"')" > $path/$certname.csr

cat $path/root-ca.crt > $path/chain.cer
cat $path/signing-ca.crt >> $path/chain.cer
cat $path/$certname.cer >> $path/chain.cer

# Create pfx
openssl pkcs12 -export -out $path/app.pfx -inkey $path/$certname.key -in $path/chain.cer -passout pass:

# Remove JSON Data
rm -Rf $path/tmpcert.json 

# Build sample app
docker rm -f testapp
docker build --rm -t testapp -f ${DIR}/../testapp/Dockerfile ${DIR}/..
docker run -d -p 8090:443 --network ssl-test --name testapp testapp

# Build trust verifier
docker build --rm -t testcerttrust -f ${DIR}/../testcerttrust/Dockerfile ${DIR}/..
docker run --rm --network ssl-test -e TestApp__Uri=https://testapp/api/values testcerttrust