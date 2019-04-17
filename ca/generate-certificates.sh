#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

certname='intermediate.local'
caaddress='localhost:8888'
path="${DIR}/../certs"

mkdir -p $path

docker build --rm -t cfssltest-rootca -f ${DIR}/../ca/RootCA.Dockerfile ${DIR}/..
docker rm -f rootca
docker run -d -p 8888:8888 --name rootca cfssltest-rootca

echo $certname
echo $caaddress

# Extract Root CA

curl -X POST $caaddress/api/v1/cfssl/info -d '{"label":"primary"}' -o $path/tmprootca.json

echo -e "$(cat $path/tmprootca.json | python -m json.tool | grep certificate | cut -f4 -d '"')" > $path/rootca.cer

rm -Rf $path/tmprootca.json

# Generate Certificate
curl -d '{
  "hosts":["'$certname'"],
  "names":[{"C":"US", "ST":"California", "L":"San Francisco", "O":"'$certname'"}],
  "CN": "www.intermediate-example.com"}' -o $path/tmpcert.json http://$caaddress/api/v1/cfssl/init_ca

# Create Private Key
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep private_key | cut -f4 -d '"')" > $path/$certname.key

# Create Certificate
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep -m 1 certificate | cut -f4 -d '"')" > $path/$certname.cer

# Remove JSON Data
rm -Rf $path/tmpcert.json 

docker rm -f intermediateca
docker build --rm -t cfssltest-intermediateca -f ${DIR}/../ca/CA.Dockerfile ${DIR}/..
docker run -d -p 8889:8888 --name intermediateca cfssltest-intermediateca

certname='test.local'
caaddress='localhost:8889'

echo $certname
echo $caaddress
# Generate Certificate
curl -d "{ \"request\": {
  \"CN\": \"$certname\",
  \"hosts\":[\"$certname\"],
  \"key\": { \"algo\": \"rsa\",\"size\": 2048 }, 
  \"names\": [{\"C\":\"US\",\"ST\":\"California\", \"L\":\"San Francisco\",\"O\":\"$certname\"}]
  }
}" -o $path/tmpcert.json http://$caaddress/api/v1/cfssl/newcert

# Create Private Key
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep private_key | cut -f4 -d '"')" > $path/$certname.key

# Create Certificate
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep -m 1 certificate | cut -f4 -d '"')" > $path/$certname.cer

# Create Certificate Request
echo -e "$(cat $path/tmpcert.json | python -m json.tool | grep certificate_request | cut -f4 -d '"')" > $path/$certname.csr

cat $path/rootca.cer > $path/chain.cer
cat $path/intermediate.local.cer >> $path/chain.cer
cat $path/$certname.cer >> $path/chain.cer

# Create pfx
openssl pkcs12 -export -out $path/app.pfx -inkey $path/$certname.key -in $path/chain.cer

# Remove JSON Data
rm -Rf $path/tmpcert.json 