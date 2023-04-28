FROM debian:stable-slim

WORKDIR /app

RUN apt-get update && apt-get install libicu-dev -y && apt-get clean && rm -rf /var/cache/apt/archives /var/lib/apt/lists/*

COPY a/* ./

ENTRYPOINT ["./BackgroundServices"]
