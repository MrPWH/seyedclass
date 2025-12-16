# مسیر پوشه Telegram TData
$SourceFolder = "C:\Users\YourUserName\AppData\Roaming\Telegram Desktop\tdata"

# مسیر و نام فایل ZIP خروجی
$DestinationZip = "F:\Backup\TelegramBackup.zip"

# فشرده‌سازی
Compress-Archive -Path $SourceFolder -DestinationPath $DestinationZip -Force

Write-Host "پوشه TData تلگرام با موفقیت فشرده شد و در مسیر مورد نظر ذخیره شد."
