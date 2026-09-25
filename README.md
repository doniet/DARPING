# DarPing

Monitor ligero para Windows inspirado en las utilidades de bandeja de NirSoft.

- Muestra descarga y subida en la bandeja y en la ventana principal.
- Comprueba `8.8.8.8` cada segundo y permite cambiar el destino.
- Verde: conectado. Amarillo: ping lento. Rojo: sin respuesta.
- Sonido corto opcional por cada comprobación.
- Guarda el histórico en `%LOCALAPPDATA%\DarPing\history.log` y lo dibuja al abrir la app.

## Compilar

En un equipo con .NET Framework 4.8 instalado:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /out:DarPing.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Net.Http.dll Program.cs AppSettings.cs MonitorService.cs HistoryStore.cs MainForm.cs
```