# Sakin Ingest Service - Dockerfile (Placeholder)
# This service is not yet implemented

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

WORKDIR /app

# Placeholder: Copy a simple script that logs the service is not ready
RUN echo '#!/bin/sh' > /app/entrypoint.sh && \
    echo 'echo "🚧 Sakin Ingest Service - Not yet implemented"' >> /app/entrypoint.sh && \
    echo 'echo "This service will handle data ingestion and normalization"' >> /app/entrypoint.sh && \
    echo 'echo "Sleeping to maintain container..."' >> /app/entrypoint.sh && \
    echo 'sleep infinity' >> /app/entrypoint.sh && \
    chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
