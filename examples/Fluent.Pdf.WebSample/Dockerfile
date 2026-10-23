# Caller must supply a pinned runtime-image digest and explicitly provision host providers/tenants.
ARG RUNTIME_IMAGE
FROM ${RUNTIME_IMAGE}
WORKDIR /app
COPY --chown=65532:65532 publish/ ./
USER 65532:65532
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "Fluent.Pdf.WebSample.dll"]
