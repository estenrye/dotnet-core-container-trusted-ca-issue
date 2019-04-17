# https://www.greenreedtech.com/building-a-lightweight-ceritifcate-authority/
FROM cfssl/cfssl:latest
COPY ./certs/signing-ca.crt ca.pem
COPY certs/signing-ca/private/signing-ca.key ca-key.pem

EXPOSE 8888

ENTRYPOINT ["cfssl"]

CMD ["serve","-ca=ca.pem","-ca-key=ca-key.pem","-address=0.0.0.0"] 