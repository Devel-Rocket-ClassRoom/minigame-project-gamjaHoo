# V0.5 이슈 누락 메타데이터 보정 스크립트
# rate limit 으로 Priority/Size 설정 실패한 이슈들을 보정.
#
# 사용법: .\scripts\fix-v0.5-issue-metadata.ps1
# 의존: gh CLI 인증, Rate limit 회복 후 실행.

$ErrorActionPreference = 'Continue'

# UTF-8 출력 인코딩 강제 (PowerShell 5.x 의 native command 출력 CP949 → UTF-8)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ProjectId       = "PVT_kwDODykJwc4BYAHm"
$PriorityFieldId = "PVTSSF_lADODykJwc4BYAHmzhTJKJg"
$SizeFieldId     = "PVTSSF_lADODykJwc4BYAHmzhTJKKY"
$PriorityOpts    = @{ P0 = "93ef17a8"; P1 = "224bd1d6"; P2 = "4eea6a89" }
$SizeOpts        = @{ XS = "e2f4dac2"; S = "ba56ccb2"; M = "0ce57aba"; L = "a6edd398"; XL = "bf501b62" }

$Repo  = "Devel-Rocket-ClassRoom/minigame-project-gamjaHoo"

# 누락 이슈 + 메타데이터 매핑 (#179~#206 전체 — idempotent)
$Fixes = @(
    @{ IssueNum = 179; Key = "A.1"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 180; Key = "A.2"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 181; Key = "A.3"; Priority = "P1"; Size = "M"  }
    @{ IssueNum = 182; Key = "A.4"; Priority = "P0"; Size = "L"  }
    @{ IssueNum = 183; Key = "A.5"; Priority = "P0"; Size = "M"  }
    @{ IssueNum = 184; Key = "B.1"; Priority = "P0"; Size = "M"  }
    @{ IssueNum = 185; Key = "B.2"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 186; Key = "B.3"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 187; Key = "B.4"; Priority = "P1"; Size = "XS" }
    @{ IssueNum = 188; Key = "B.5"; Priority = "P1"; Size = "S"  }
    @{ IssueNum = 189; Key = "C.1"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 190; Key = "C.2"; Priority = "P1"; Size = "S"  }
    @{ IssueNum = 191; Key = "C.3"; Priority = "P1"; Size = "XS" }
    @{ IssueNum = 192; Key = "D.1"; Priority = "P0"; Size = "M"  }
    @{ IssueNum = 193; Key = "D.2"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 194; Key = "D.3"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 195; Key = "D.4"; Priority = "P0"; Size = "L"  }
    @{ IssueNum = 196; Key = "D.5"; Priority = "P1"; Size = "M"  }
    @{ IssueNum = 197; Key = "E.1"; Priority = "P0"; Size = "S"  }
    @{ IssueNum = 198; Key = "E.2"; Priority = "P0"; Size = "M"  }
    @{ IssueNum = 199; Key = "E.3"; Priority = "P0"; Size = "M"  }
    @{ IssueNum = 200; Key = "E.4"; Priority = "P1"; Size = "M"  }
    @{ IssueNum = 201; Key = "F.1"; Priority = "P0"; Size = "L"  }
    @{ IssueNum = 202; Key = "F.2"; Priority = "P0"; Size = "XS" }
    @{ IssueNum = 203; Key = "G.1"; Priority = "P0"; Size = "L"  }
    @{ IssueNum = 204; Key = "G.2"; Priority = "P0"; Size = "L"  }
    @{ IssueNum = 205; Key = "G.3"; Priority = "P1"; Size = "S"  }
    @{ IssueNum = 206; Key = "G.4"; Priority = "P1"; Size = "S"  }
)

# GraphQL queries (variables 형식)
$AddQuery = 'mutation($p: ID!, $c: ID!) { addProjectV2ItemById(input: { projectId: $p, contentId: $c }) { item { id } } }'
$UpdQuery = 'mutation($p: ID!, $i: ID!, $f: ID!, $o: String!) { updateProjectV2ItemFieldValue(input: { projectId: $p, itemId: $i, fieldId: $f, value: { singleSelectOptionId: $o } }) { projectV2Item { id } } }'

foreach ($fix in $Fixes) {
    Write-Host "[$($fix.Key)] #$($fix.IssueNum)" -ForegroundColor Cyan

    # 1. 이슈 node_id 가져오기 (정규식 추출 — ASCII 필드, 한글 인코딩 무관)
    $issueJson = (gh api "repos/$Repo/issues/$($fix.IssueNum)") -join "`n"
    if ($issueJson -match '"node_id"\s*:\s*"([^"]+)"') {
        $nodeId = $matches[1]
    } else {
        Write-Host "  ERROR: node_id not found in response" -ForegroundColor Red
        continue
    }

    # 2. 보드 추가 (정규식 추출)
    $addJson = (gh api graphql -f "p=$ProjectId" -f "c=$nodeId" -f "query=$AddQuery") -join "`n"
    if ($addJson -match '"id"\s*:\s*"(PVTI_[^"]+)"') {
        $itemId = $matches[1]
    } else {
        Write-Host "  ERROR: itemId not obtained (resp: $addJson)" -ForegroundColor Red
        continue
    }
    Write-Host "  Item ID: $itemId" -ForegroundColor DarkGray

    # 3. Priority + Size (variables 형식, 정규식 검증)
    $priOpt = $PriorityOpts[$fix.Priority]
    $sizeOpt = $SizeOpts[$fix.Size]

    $priJson = (gh api graphql -f "p=$ProjectId" -f "i=$itemId" -f "f=$PriorityFieldId" -f "o=$priOpt" -f "query=$UpdQuery") -join "`n"
    $sizeJson = (gh api graphql -f "p=$ProjectId" -f "i=$itemId" -f "f=$SizeFieldId" -f "o=$sizeOpt" -f "query=$UpdQuery") -join "`n"

    $priOK  = $priJson  -match [regex]::Escape($itemId)
    $sizeOK = $sizeJson -match [regex]::Escape($itemId)

    if ($priOK -and $sizeOK) {
        Write-Host "  Priority=$($fix.Priority) Size=$($fix.Size) set" -ForegroundColor Green
    } else {
        Write-Host "  WARN: priOK=$priOK sizeOK=$sizeOK" -ForegroundColor Yellow
    }

    Start-Sleep -Milliseconds 200
}

Write-Host ""
Write-Host "보정 완료: $($Fixes.Count) 이슈" -ForegroundColor Cyan
