FROM ubuntu:rolling

RUN apt-get update && apt-get upgrade -y && apt-get install libicu-dev -y && apt-get clean autoclean && apt-get autoremove --yes && rm -rf /var/lib/{apt,dpkg,cache,log}/

WORKDIR /app

COPY publish/* ./

ENTRYPOINT ["./ExpiredKeyRemover"]
