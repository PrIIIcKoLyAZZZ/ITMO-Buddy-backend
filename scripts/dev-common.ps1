param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('up', 'down', 'logs')]
    [string] $Action,
    [string] $Service
)

function Fail([string] $Message) {
    throw $Message
}

function Invoke-Compose {
    & docker @ComposeArgs @args
}

function Get-ServiceContainer([string] $Name) {
    $id = & docker ps -aq --filter "label=com.docker.compose.project=$Project" `
        --filter "label=com.docker.compose.service=$Name" --filter 'label=com.docker.compose.oneoff=False'
    if ($LASTEXITCODE -ne 0) { Fail "Could not inspect $Name." }
    return $id
}

function Require-Env {
    if (-not [IO.File]::Exists((Join-Path $Root '.env.docker'))) {
        Fail 'Missing .env.docker. Run scripts\dev-up.bat first.'
    }
    Invoke-Compose config -q 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Fail 'Invalid .env.docker or Compose YAML. Check the required values against .env.docker.example.'
    }
}

function Read-Value([string[]] $Lines, [string] $Key) {
    $prefix = "$Key="
    $values = @($Lines | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) })
    if ($values.Count -ne 1) { Fail "Missing or ambiguous $Key." }
    return $values[0].Substring($prefix.Length)
}

function New-RandomBytes([int] $Count) {
    $bytes = New-Object byte[] $Count
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }
    return ,$bytes
}

function New-Hex([int] $Count) {
    return [BitConverter]::ToString((New-RandomBytes $Count)).Replace('-', '').ToLowerInvariant()
}

function New-DockerEnv {
    if (Test-Path -LiteralPath '.env' -PathType Leaf) {
        $settings = & docker compose --env-file .env -p $Project -f compose.yml config --environment 2>$null
        if ($LASTEXITCODE -ne 0) { Fail 'Cannot read PostgreSQL settings from .env. Set the working POSTGRES_* values in .env.docker.' }
    }
    elseif ($VolumeExists) {
        $id = Get-ServiceContainer postgres
        if (-not $id) { Fail 'Existing PostgreSQL volume has no container or .env. Set the working POSTGRES_* values in .env.docker.' }
        $settings = & docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' $id
        if ($LASTEXITCODE -ne 0) { Fail 'Could not read existing PostgreSQL settings.' }
    }
    else {
        $settings = @('POSTGRES_USER=itmo_buddy', 'POSTGRES_DB=itmo_buddy', "POSTGRES_PASSWORD=$(New-Hex 24)")
    }
    $pgUser = Read-Value $settings 'POSTGRES_USER'
    $pgDb = Read-Value $settings 'POSTGRES_DB'
    $pgPassword = Read-Value $settings 'POSTGRES_PASSWORD'
    if ($pgUser -cnotmatch '^[A-Za-z0-9_]+$' -or $pgDb -cnotmatch '^[A-Za-z0-9_]+$') {
        Fail 'Set POSTGRES_USER and POSTGRES_DB as simple PostgreSQL identifiers in .env.docker.'
    }
    if (-not $pgPassword -or $pgPassword -like 'REPLACE_*' -or $pgPassword.Contains("'") -or $pgPassword.Contains('\')) {
        Fail 'Set the working POSTGRES_PASSWORD in .env.docker using Compose dotenv quoting.'
    }
    $temporary = Join-Path $Root ('.env.docker.' + [Guid]::NewGuid())
    try {
        [IO.File]::WriteAllLines($temporary, @(
            "POSTGRES_USER='$pgUser'"
            "POSTGRES_DB='$pgDb'"
            "POSTGRES_PASSWORD='$pgPassword'"
            "JWT_SIGNING_KEY=$([Convert]::ToBase64String((New-RandomBytes 48)))"
            "MINIO_ROOT_USER=itmo_$(New-Hex 12)"
            "MINIO_ROOT_PASSWORD=$(New-Hex 24)"
            'MINIO_BUCKET=itmo-buddy-local'
            'CORS_ORIGIN_0=https://localhost:5173'
            'CORS_ORIGIN_1=https://localhost:3000'
        ), [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, (Join-Path $Root '.env.docker'))
    }
    finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
    Write-Host 'Created .env.docker with local credentials.'
}

function Assert-Settings([string[]] $Lines) {
    foreach ($key in @('POSTGRES_USER','POSTGRES_DB','POSTGRES_PASSWORD','JWT_SIGNING_KEY','MINIO_ROOT_USER','MINIO_ROOT_PASSWORD','MINIO_BUCKET')) {
        $value = Read-Value $Lines $key
        if (-not $value -or $value -like 'REPLACE_*') { Fail "Set $key in .env.docker." }
        switch ($key) {
            { $_ -in 'POSTGRES_USER','POSTGRES_DB' } {
                if ($value -cnotmatch '^[A-Za-z0-9_]+$') { Fail "$key must be a simple PostgreSQL identifier." }
            }
            'JWT_SIGNING_KEY' {
                try { $size = [Convert]::FromBase64String($value).Length }
                catch { $size = 0 }
                if ($size -lt 32) { Fail 'JWT_SIGNING_KEY must decode from Base64 to at least 32 bytes.' }
            }
            'MINIO_ROOT_USER' {
                if ($value.Length -lt 3) { Fail 'MINIO_ROOT_USER needs at least 3 characters.' }
            }
            'MINIO_ROOT_PASSWORD' {
                if ($value.Length -lt 8) { Fail 'MINIO_ROOT_PASSWORD needs at least 8 characters.' }
            }
            'MINIO_BUCKET' {
                if ($value -cnotmatch '^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$' -or
                    $value.Contains('..') -or $value -match '^\d+\.\d+\.\d+\.\d+$') {
                    Fail 'MINIO_BUCKET must be a valid lowercase S3 bucket name.'
                }
            }
        }
    }
}

function Assert-Ports {
    $publish = @()
    foreach ($mapping in 'postgres:2438:5432','caddy:8443:8443','caddy:9443:9443') {
        $name, $hostPort, $servicePort = $mapping.Split(':')
        $id = Get-ServiceContainer $name
        $binding = '127.0.0.1:{0}' -f $hostPort
        if ($id) {
            $actual = & docker port $id "$servicePort/tcp" 2>$null
            if ($LASTEXITCODE -eq 0 -and $actual -contains $binding) { continue }
        }
        $publish += @('-p', ('{0}:{1}' -f $binding, $servicePort))
    }
    if ($publish.Count -gt 0) {
        & docker run --rm @publish --entrypoint /bin/sh $ProbeImage -c true
        if ($LASTEXITCODE -ne 0) {
            Fail 'Could not reserve an expected loopback port (2438, 8443, 9443). Check the Docker error and stop only the conflicting service.'
        }
    }
}

function Assert-ExistingPostgres([string[]] $Lines) {
    Invoke-Compose up -d --no-recreate postgres
    if ($LASTEXITCODE -ne 0) { Fail 'Could not start existing PostgreSQL.' }
    $id = Get-ServiceContainer postgres
    $mountJson = & docker inspect --format '{{json .Mounts}}' $id
    if ($LASTEXITCODE -ne 0) { Fail 'Could not inspect existing PostgreSQL mounts.' }
    $mounts = $mountJson | ConvertFrom-Json
    $mount = @($mounts) | Where-Object { $_.Destination -eq '/var/lib/postgresql/data' }
    if ($mount.Name -ne "${Project}_postgres_data") { Fail 'Existing PostgreSQL container uses an unexpected volume.' }
    $user = Read-Value $Lines 'POSTGRES_USER'
    $db = Read-Value $Lines 'POSTGRES_DB'
    $previousPassword = $env:PGPASSWORD
    $env:PGPASSWORD = Read-Value $Lines 'POSTGRES_PASSWORD'
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    try {
        do {
            & docker exec -e PGPASSWORD -e PGCONNECT_TIMEOUT=3 $id psql -X -w -h postgres -U $user -d $db -Atqc 'SELECT 1' 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { return }
            $state = & docker inspect --format '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}' $id
            if ($state -ne 'running starting') { break }
            Start-Sleep -Seconds 2
        } while ([DateTime]::UtcNow -lt $deadline)
        Fail 'Cannot authenticate to existing PostgreSQL. Use its working POSTGRES_* values in .env.docker and inspect postgres logs. The volume was preserved.'
    }
    finally { $env:PGPASSWORD = $previousPassword }
}

function Wait-Ready([string] $TemporaryCa) {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    do {
        $jobsReady = $true
        foreach ($job in 'migrator','bucket-init') {
            $id = Get-ServiceContainer $job
            if (-not $id) { $jobsReady = $false; break }
            $state = & docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' $id
            if ($state -eq 'exited 0') { continue }
            if ($state -like 'exited*') { Fail "$job failed. See scripts\dev-logs.bat $job." }
            $jobsReady = $false
            break
        }
        if ($jobsReady) {
            $pg = Get-ServiceContainer postgres
            $minio = Get-ServiceContainer minio
            $caddy = Get-ServiceContainer caddy
            if ($pg -and $minio -and $caddy -and
                (& docker inspect --format '{{.State.Health.Status}}' $pg) -eq 'healthy' -and
                (& docker inspect --format '{{.State.Health.Status}}' $minio) -eq 'healthy') {
                $certificate = & docker exec $caddy cat /data/caddy/pki/authorities/local/root.crt 2>$null
                if ($LASTEXITCODE -eq 0 -and $certificate -contains '-----BEGIN CERTIFICATE-----') {
                    [IO.File]::WriteAllLines($TemporaryCa, [string[]]$certificate, [Text.Encoding]::ASCII)
                    $curlArgs = @('--noproxy', '*', '-s', '--max-time', '5', '-o')
                    if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
                        $code = & curl.exe @curlArgs NUL -w '%{http_code}' --cacert $TemporaryCa https://localhost:8443/health/ready
                    }
                    else {
                        $code = & docker run --rm --user 0:0 --network "container:$caddy" `
                            --mount "type=bind,source=$TemporaryCa,target=/root-ca.crt,readonly" $ProbeImage `
                            @curlArgs /dev/null -w '%{http_code}' --cacert /root-ca.crt https://localhost:8443/health/ready
                    }
                    if ($LASTEXITCODE -eq 0 -and $code -eq '200') { return }
                }
            }
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    Invoke-Compose ps -a | Out-Host
    Fail 'Timed out waiting for HTTPS readiness. See scripts\dev-logs.bat api or caddy.'
}

function Publish-Ca {
    $dir = Join-Path $Root '.docker-local'
    [IO.Directory]::CreateDirectory($dir) | Out-Null
    $ca = Join-Path $dir 'root.crt'
    $trusted = Join-Path $dir 'trusted-root.crt'
    $temporary = Join-Path $dir ("root." + [Guid]::NewGuid())
    try {
        Wait-Ready $temporary
        $hash = (Get-FileHash -LiteralPath $temporary).Hash
        if ([IO.File]::Exists($ca) -and $hash -ne (Get-FileHash -LiteralPath $ca).Hash) {
            Write-Warning 'The Caddy CA changed. Remove trust for the old CA before trusting the new one (see README).'
        }
        [IO.File]::Copy($temporary, $ca, $true)
    }
    finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
    Write-Host "Public Caddy CA: $ca"
    $installed = [IO.File]::Exists($trusted) -and $hash -eq (Get-FileHash -LiteralPath $trusted).Hash
    if (-not $installed -and [Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
        Write-Host 'Trusting this local root CA lets certificates it signs be trusted on this computer.'
        if ((Read-Host 'Install trust for the ITMO Buddy local CA for the current user? [y/N]') -in 'y','yes') {
            & certutil.exe -user -addstore Root $ca | Out-Host
            if ($LASTEXITCODE -eq 0) {
                [IO.File]::Copy($ca, $trusted, $true)
                $installed = $true
            }
            else { Write-Warning 'Trust installation failed. See the manual CA instructions in README.' }
        }
    }
    if (-not $installed) { Write-Host 'The backend works. Your browser may still require trust for the local CA (see README).' }
}

try {
    $Root = Split-Path -Parent $PSScriptRoot
    Set-Location -LiteralPath $Root -ErrorAction Stop
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Install Docker Desktop or Docker Engine with Compose V2.' }
    & docker compose version 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'Docker Compose V2 is unavailable.' }
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'Docker daemon is unavailable. Start Docker, then check docker info.' }
    if (-not ((Test-Path compose.yml) -and (Test-Path compose.frontend.yml) -and (Test-Path docker/Caddyfile))) {
        Fail 'Compose files or docker/Caddyfile are missing.'
    }
    $match = Select-String -LiteralPath compose.yml -Pattern '^name:\s+([a-z0-9_-]+)\s*$' | Select-Object -First 1
    if (-not $match) { Fail 'Missing Compose project name in compose.yml.' }
    $Project = $match.Matches[0].Groups[1].Value
    $ComposeArgs = @('compose','--env-file','.env.docker','-p',$Project,'-f','compose.yml','-f','compose.frontend.yml')
    foreach ($key in 'POSTGRES_USER','POSTGRES_PASSWORD','POSTGRES_DB','JWT_SIGNING_KEY','MINIO_ROOT_USER','MINIO_ROOT_PASSWORD',
        'MINIO_BUCKET','CORS_ORIGIN_0','CORS_ORIGIN_1','CADDY_SUBNET','CADDY_DYNAMIC_RANGE','CADDY_IP') {
        [Environment]::SetEnvironmentVariable($key, $null, 'Process')
    }
    switch ($Action) {
        'down' {
            Require-Env
            Invoke-Compose down
            exit $LASTEXITCODE
        }
        'logs' {
            Require-Env
            if ($Service -and $Service -cnotin 'api','caddy','postgres','minio','migrator','bucket-init') {
                Fail 'Usage: dev-logs.bat [api|caddy|postgres|minio|migrator|bucket-init]'
            }
            $services = @()
            if ($Service) { $services += $Service }
            Invoke-Compose logs --tail=100 --follow @services
            exit $LASTEXITCODE
        }
    }
    $ProbeImage = 'curlimages/curl:8.17.0'
    & docker volume inspect "${Project}_postgres_data" 2>$null | Out-Null
    $VolumeExists = $LASTEXITCODE -eq 0
    $match = Select-String -LiteralPath compose.yml -Pattern '^\s+container_name:\s+(\S+)\s*$' | Select-Object -First 1
    if ($match) {
        $postgresName = $match.Matches[0].Groups[1].Value
        $labels = & docker inspect --type=container --format '{{json .Config.Labels}}' $postgresName 2>$null
        if ($LASTEXITCODE -eq 0) {
            $owner = $labels | ConvertFrom-Json
            if ($owner.'com.docker.compose.project' -ne $Project -or $owner.'com.docker.compose.service' -ne 'postgres') {
                Fail "Container $postgresName belongs to a different Compose project. Check its ownership before continuing."
            }
        }
    }
    if (-not (Test-Path -LiteralPath '.env.docker')) { New-DockerEnv }
    Require-Env
    $settings = Invoke-Compose config --environment 2>$null
    if ($LASTEXITCODE -ne 0) { Fail 'Could not read .env.docker.' }
    Assert-Settings $settings
    Assert-Ports
    if ($VolumeExists) { Assert-ExistingPostgres $settings }
    Remove-Variable settings
    Invoke-Compose up -d --build --wait --wait-timeout 120
    if ($LASTEXITCODE -ne 0) {
        Invoke-Compose ps -a | Out-Host
        Fail 'Compose startup failed. See scripts\dev-logs.bat [service]. For an overlapping Docker subnet, adjust CADDY_SUBNET, CADDY_DYNAMIC_RANGE and CADDY_IP together.'
    }
    Publish-Ca
    Write-Host @'
ITMO Buddy Backend is ready.

Backend:
https://localhost:8443

Swagger UI:
https://localhost:8443/swagger

OpenAPI:
https://localhost:8443/openapi/v1.json

Health:
https://localhost:8443/health/ready

SignalR:
https://localhost:8443/hubs/chat

MinIO signed URLs:
https://localhost:9443

PostgreSQL:
127.0.0.1:2438
'@
    exit 0
}
catch {
    [Console]::Error.WriteLine("Error: $($_.Exception.Message)")
    exit 1
}
