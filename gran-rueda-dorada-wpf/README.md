# Gran Rueda Dorada (C# / .NET 8 / WPF)

Tragamonedas de 3 carretes con rueda de bonificación, al estilo de las máquinas de casino de rodillos con rueda en la parte superior. Todo el arte es vectorial y se dibuja en tiempo de ejecución, y todos los sonidos se sintetizan al iniciar: el proyecto no tiene imágenes ni archivos de audio ni dependencias NuGet.

## Requisitos

- Windows 10 u 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o Visual Studio 2022 con la carga de trabajo "Desarrollo de escritorio de .NET")

## Ejecutar

```powershell
cd gran-rueda-dorada-wpf
dotnet run --project src/GranRuedaDorada
```

O abre `GranRuedaDorada.sln` en Visual Studio y pulsa F5.

Para generar un ejecutable independiente (no requiere .NET instalado):

```powershell
dotnet publish src/GranRuedaDorada -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Controles

| Acción | Botón | Teclado |
|---|---|---|
| Girar / completar el conteo de un premio / girar la rueda | GIRAR | Espacio o Enter |
| Cambiar apuesta (1, 2, 3) | APOSTAR 1 | B o ↑ |
| Apostar 3 y girar | APUESTA MÁXIMA | M |
| Tabla de pagos | PAGOS | P (Esc para cerrar) |
| Sonido | botón del altavoz | — |

También puedes hacer clic en la rueda para girarla cuando está activo el bono. Si te quedas sin créditos, GIRAR cambia a RECARGAR (1000 créditos de demostración). Los créditos se guardan en `%APPDATA%\GranRuedaDorada`.

## Qué incluye

- **Rueda de bonificación** de 22 casillas (25 a 1000) con clavijas, puntero que rebota en cada clavija con su clic, focos que parpadean, luz trasera y casilla ganadora destellando.
- **Carretes mecánicos**: pequeño retroceso al arrancar, desenfoque vertical de movimiento, frenado y rebote al detenerse, cristal curvo con sombreado.
- **Suspenso en el tercer carrete** cuando puede dar el bono o el premio mayor: el carrete gira más tiempo, su marco destella y suena un tono ascendente.
- **Celebraciones según el tamaño del premio**: campanas y conteo del medidor; para premios medianos, luces y destellos del gabinete; para grandes premios, fanfarria de metales, aplausos, lluvia de monedas y pantalla de "¡GRAN PREMIO!" o "¡SÚPER PREMIO!".
- **Medidores LED de 7 segmentos** con los segmentos apagados visibles, como en las máquinas reales.
- **Sonidos sintetizados**: motor y trinquete de los carretes, golpe de frenado, clic de la rueda, campanas del conteo, fanfarrias y aplausos.

## Matemática

Carretes de 22 paradas con pesos virtuales (los espacios en blanco caen más a menudo), como en las máquinas de rodillos. El RNG usa `RandomNumberGenerator` (criptográfico).

| Apuesta | Retorno teórico | Frecuencia de premio | Bono de rueda |
|---|---|---|---|
| 3 créditos | ~94,5 % | ~22 % | 1 de cada ~44 giros |
| 1 crédito | ~61 % | ~22 % | — |

Las cifras se calculan exactamente enumerando todas las combinaciones (`SlotEngine.Analyze`) y la tabla de pagos del juego las muestra.

## Estructura

```
src/GranRuedaDorada/
  Engine/SlotEngine.cs      reglas, carretes, pagos, rueda, RNG y cálculo de retorno
  Rendering/Art.cs          símbolos y pinceles vectoriales
  Controls/ReelView.cs      carrete con animación y desenfoque
  Controls/WheelView.cs     rueda, clavijas, puntero
  Controls/Lights.cs        focos del letrero y de la rueda
  Controls/LedDisplay.cs    medidores LED de 7 segmentos
  Controls/CoinShower.cs    lluvia de monedas
  Audio/Synth.cs            síntesis de todos los efectos de sonido
  Audio/SoundBank.cs        reproducción simultánea de sonidos
  MainWindow.xaml(.cs)      gabinete y flujo del juego
tests/GranRuedaDorada.Tests pruebas del motor y del sintetizador (multiplataforma)
```

Pruebas: `dotnet test`.

Juego de demostración con créditos ficticios. No usa dinero real. Los nombres, el arte y los sonidos son originales y no pertenecen a ninguna marca comercial.
