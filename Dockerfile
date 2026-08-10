# A container that runs the MCP server over stdio.
#
# Nothing in this project needs Docker to work -- `dnx XafLogicExplainer.Mcp` runs the same server
# with nothing installed. This exists because MCP directories run automated safety and quality
# checks against a container, and because it is the shortest way to try the server without a .NET
# SDK on the machine.
#
#   docker build -t xaflogic-mcp .
#   docker run --rm -i xaflogic-mcp                        # answers about the bundled demo app
#   docker run --rm -i -v /path/to/MyApp.Module:/app/xaf:ro \
#     -e XAFLOGIC_PROJECT=/app/xaf xaflogic-mcp            # answers about your application

# ------------------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Only the two projects the server actually needs. The solution also contains a Blazor widget that
# references DevExpress.ExpressApp.Blazor, which lives on the licensed DevExpress feed -- restoring
# the whole solution here would demand credentials nobody outside a subscription has.
#
# The csproj files are copied on their own first so that editing source does not invalidate the
# restore layer.
COPY Directory.Build.props README.md ./
COPY assets/ ./assets/
COPY src/XafLogicExplainer.Core/XafLogicExplainer.Core.csproj src/XafLogicExplainer.Core/
COPY src/XafLogicExplainer.Mcp/XafLogicExplainer.Mcp.csproj src/XafLogicExplainer.Mcp/
RUN dotnet restore src/XafLogicExplainer.Mcp/XafLogicExplainer.Mcp.csproj

COPY src/ ./src/
RUN dotnet publish src/XafLogicExplainer.Mcp/XafLogicExplainer.Mcp.csproj \
        --no-restore -c Release -o /app

# --------------------------------------------------------------------------- run
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

# The server reads an XAF module and exits if it cannot find one, so the image ships with the demo
# application from the repository. It is synthetic, belongs to no client, references no DevExpress
# assembly, and gives every tool something real to answer with -- which is what makes an automated
# check meaningful rather than a test of the error path.
#
# Mount your own module over XAFLOGIC_PROJECT to point it at a real application.
COPY tests/XafLogicExplainer.Tests/Fixtures/DemoSolution/ /demo/
ENV XAFLOGIC_PROJECT=/demo/PharmacyDemo.Module

LABEL org.opencontainers.image.title="XAF Logic Explainer MCP server" \
      org.opencontainers.image.description="Query a specific DevExpress XAF application over the Model Context Protocol." \
      org.opencontainers.image.source="https://github.com/peopleworks/XAFLogicExplainer" \
      org.opencontainers.image.licenses="MIT"

# Extraction is Roslyn over local source files: the server never writes anywhere and never opens a
# socket, so it has no reason to run as root.
USER $APP_UID

# stdout is the JSON-RPC channel. The server writes every message of its own to stderr, so nothing
# here may add output to stdout.
ENTRYPOINT ["dotnet", "/app/xaflogic-mcp.dll"]
