# V1.0 중복 이슈 정리 — 일회성 스크립트.
# 새 이슈 (중복) 에 duplicate comment 추가.
# 기존 일괄 생성 이슈 (실제 작업 완료) 에 completed comment + close.

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# 매핑: [작업 Key, 새 이슈 (중복), 기존 이슈 (정식), 머지된 PR 번호]
$Pairs = @(
    @{ Key = "B.4"; New = 274; Original = 187; PR = 275 }
    @{ Key = "B.5"; New = 276; Original = 188; PR = 277 }
    @{ Key = "C.1"; New = 278; Original = 189; PR = 281 }
    @{ Key = "C.2"; New = 279; Original = 190; PR = 281 }
    @{ Key = "C.3"; New = 280; Original = 191; PR = 281 }
    @{ Key = "D.2"; New = 282; Original = 193; PR = 285 }
    @{ Key = "D.3"; New = 283; Original = 194; PR = 285 }
    @{ Key = "D.5"; New = 284; Original = 196; PR = $null }  # D.5 = 진행 중, PR null
)

foreach ($p in $Pairs) {
    Write-Host "[$($p.Key)] 새=#$($p.New) → 기존=#$($p.Original)" -ForegroundColor Cyan

    # 1. 새 이슈에 duplicate comment 추가
    $newComment = "Duplicate of #$($p.Original) — V1.0 일괄 생성 이슈 (#$($p.Original)) 가 정식. 본 이슈는 실수로 별도 생성된 것."
    gh issue comment $p.New --body $newComment 2>&1 | Out-Null
    Write-Host "  [#$($p.New)] duplicate comment OK" -ForegroundColor DarkGray

    # 2. #284 는 아직 open 이라 close 처리. 나머지는 이미 closed.
    if ($p.New -eq 284) {
        gh issue close 284 --reason "not planned" 2>&1 | Out-Null
        Write-Host "  [#284] closed" -ForegroundColor DarkGray
    }

    # 3. 기존 이슈에 completed comment + close (D.5 의 #196 은 진행 중이라 제외)
    if ($p.Original -ne 196) {
        $origComment = if ($p.PR) {
            "Completed by PR #$($p.PR) — 작업이 사실상 중복 이슈 #$($p.New) 으로 진행되었음. 본 이슈로 추적 정리."
        } else {
            "Completed — 중복 이슈 #$($p.New) 로 작업 진행 후 본 이슈로 추적 정리."
        }
        gh issue comment $p.Original --body $origComment 2>&1 | Out-Null
        gh issue close $p.Original --reason "completed" 2>&1 | Out-Null
        Write-Host "  [#$($p.Original)] completed comment + close" -ForegroundColor Green
    } else {
        Write-Host "  [#$($p.Original)] D.5 진행 중 — close 안 함 (PR 본문에서 Closes)" -ForegroundColor Yellow
    }

    Start-Sleep -Milliseconds 200
}

Write-Host ""
Write-Host "정리 완료" -ForegroundColor Cyan
