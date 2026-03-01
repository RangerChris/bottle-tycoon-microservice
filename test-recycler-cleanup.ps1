# Test script to verify recycler telemetry cleanup after sale

Write-Host "=== Testing Recycler Telemetry Cleanup ===" -ForegroundColor Cyan

# Step 1: Query current recyclers in Prometheus
Write-Host "`n1. Querying Prometheus for current recyclers..." -ForegroundColor Yellow
$response = Invoke-RestMethod -Uri "http://localhost:9090/api/v1/query?query=recycler_current_bottles" -Method Get
$beforeSale = $response.data.result
Write-Host "Recyclers before sale:"
$beforeSale | ForEach-Object {
    Write-Host "  - $($_.metric.recycler_name) (ID: $($_.metric.recycler_id)): $($_.value[1]) bottles" -ForegroundColor Green
}

if ($beforeSale.Count -eq 0) {
    Write-Host "  No recyclers found. Please buy some recyclers first." -ForegroundColor Red
    exit
}

# Step 2: Instructions for user
Write-Host "`n2. ACTION REQUIRED:" -ForegroundColor Yellow
Write-Host "   Go to http://localhost:3000 and sell ONE recycler" -ForegroundColor White
Write-Host "   Press ENTER after you've sold a recycler..." -ForegroundColor White
Read-Host

# Step 3: Check backend telemetry store
Write-Host "`n3. Checking RecyclerService logs..." -ForegroundColor Yellow
$logs = docker logs bottle-tycoon-microservice-recyclerservice-1 --tail=20 2>&1 | Select-String "Sold"
if ($logs) {
    Write-Host "  Sale detected in logs:" -ForegroundColor Green
    $logs | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "  No sale detected in recent logs" -ForegroundColor Red
}

# Step 4: Wait for Prometheus scrape (10 seconds + 5 second buffer)
Write-Host "`n4. Waiting 15 seconds for Prometheus to scrape..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Step 5: Query Prometheus again
Write-Host "`n5. Querying Prometheus again..." -ForegroundColor Yellow
$response2 = Invoke-RestMethod -Uri "http://localhost:9090/api/v1/query?query=recycler_current_bottles" -Method Get
$afterSale = $response2.data.result
Write-Host "Recyclers after sale + scrape:"
$afterSale | ForEach-Object {
    Write-Host "  - $($_.metric.recycler_name) (ID: $($_.metric.recycler_id)): $($_.value[1]) bottles" -ForegroundColor Green
}

# Step 6: Compare
Write-Host "`n6. Comparison:" -ForegroundColor Yellow
$beforeCount = $beforeSale.Count
$afterCount = $afterSale.Count
Write-Host "  Before: $beforeCount recyclers" -ForegroundColor White
Write-Host "  After:  $afterCount recyclers" -ForegroundColor White

if ($afterCount -eq ($beforeCount - 1)) {
    Write-Host "`n  ✓ SUCCESS: Sold recycler was removed from metrics!" -ForegroundColor Green
} else {
    Write-Host "`n  ✗ FAILURE: Recycler count didn't decrease as expected" -ForegroundColor Red

    # Check if any recyclers disappeared
    $beforeIds = $beforeSale | ForEach-Object { $_.metric.recycler_id }
    $afterIds = $afterSale | ForEach-Object { $_.metric.recycler_id }
    $removed = $beforeIds | Where-Object { $afterIds -notcontains $_ }
    $added = $afterIds | Where-Object { $beforeIds -notcontains $_ }

    if ($removed) {
        Write-Host "`n  Removed recycler IDs:" -ForegroundColor Yellow
        $removed | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
    }
    if ($added) {
        Write-Host "`n  Added recycler IDs (unexpected):" -ForegroundColor Yellow
        $added | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
    }
}

# Step 7: Check Grafana dashboard
Write-Host "`n7. Checking Grafana dashboard query..." -ForegroundColor Yellow
$headers = @{
    'Authorization' = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('admin:admin'))
}
$grafanaQuery = "max by (recycler_name) (recycler_current_bottles{job=`"recyclerservice`"} and (timestamp(recycler_current_bottles{job=`"recyclerservice`"}) > (time() - 30)))"
$encodedQuery = [System.Web.HttpUtility]::UrlEncode($grafanaQuery)
$grafanaResponse = Invoke-RestMethod -Uri "http://localhost:3001/api/datasources/proxy/1/api/v1/query?query=$encodedQuery" -Headers $headers -Method Get

Write-Host "Grafana dashboard shows:"
$grafanaResponse.data.result | ForEach-Object {
    Write-Host "  - $($_.metric.recycler_name): $($_.value[1]) bottles" -ForegroundColor Green
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan