FROM --platform=$BUILDPLATFORM golang:1.24.8-bookworm AS toolchain
ENV CGO_ENABLED=0 GOTOOLCHAIN=local

FROM toolchain AS minio-build
WORKDIR /src
RUN git init -q \
    && git fetch --depth=1 https://github.com/minio/minio.git refs/tags/RELEASE.2025-10-15T17-29-55Z \
    && git checkout --detach FETCH_HEAD \
    && test "$(git rev-parse HEAD)" = 9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a
ARG TARGETOS TARGETARCH
RUN --mount=type=cache,target=/go/pkg/mod --mount=type=cache,target=/root/.cache/go-build \
    flags="$(MINIO_RELEASE=RELEASE go run buildscripts/gen-ldflags.go 2025-10-15T17:29:55Z)" \
    && GOOS="$TARGETOS" GOARCH="$TARGETARCH" go build -mod=readonly -trimpath -tags kqueue -ldflags "$flags" -o /out/minio .

FROM toolchain AS mc-build
WORKDIR /src
RUN git init -q \
    && git fetch --depth=1 https://github.com/minio/mc.git refs/tags/RELEASE.2025-08-13T08-35-41Z \
    && git checkout --detach FETCH_HEAD \
    && test "$(git rev-parse HEAD)" = 7394ce0dd2a80935aded936b09fa12cbb3cb8096
ARG TARGETOS TARGETARCH
RUN --mount=type=cache,target=/go/pkg/mod --mount=type=cache,target=/root/.cache/go-build \
    flags="$(MC_RELEASE=RELEASE go run buildscripts/gen-ldflags.go 2025-08-13T08:35:41Z)" \
    && GOOS="$TARGETOS" GOARCH="$TARGETARCH" go build -mod=readonly -trimpath -tags kqueue -ldflags "$flags" -o /out/mc .

FROM alpine:3.22.2 AS mc
COPY --from=mc-build /out/mc /usr/local/bin/
ENV HOME=/tmp MC_CONFIG_DIR=/tmp/mc
USER 10001:10001
ENTRYPOINT ["mc"]

FROM alpine:3.22.2 AS minio
COPY --from=minio-build /out/minio /usr/local/bin/
RUN mkdir /data && chown 10001:10001 /data
ENV HOME=/tmp MINIO_UPDATE=off
USER 10001:10001
ENTRYPOINT ["minio"]
