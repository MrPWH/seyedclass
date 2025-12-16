# نام کاربری فعلی ویندوز
$UserName = $env:USERNAME

# مسیر پوشه Telegram TData
$SourceFolder = "C:\Users\$UserName\AppData\Roaming\Telegram Desktop\tdata"

# مسیر و نام فایل ZIP خروجی
$DestinationZip = "F:\TelegramBackup.zip"

# فشرده‌سازی
Compress-Archive -Path $SourceFolder -DestinationPath $DestinationZip -Force

Write-Host "پوشه TData تلگرام با موفقیت فشرده شد و در مسیر مورد نظر ذخیره شد."
