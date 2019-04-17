# https://www.greenreedtech.com/building-a-lightweight-ceritifcate-authority/
FROM cfssl/cfssl:latest
COPY ./certs/intermediate.local.cer ca.pem
COPY certs/intermediate.local.key ca-key.pem

EXPOSE 8888

ENTRYPOINT ["cfssl"]

CMD ["serve","-ca=ca.pem","-ca-key=ca-key.pem","-address=0.0.0.0"] 