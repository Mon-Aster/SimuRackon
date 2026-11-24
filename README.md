# SimuRackon

Simulador educativo de control de tráfico aéreo en C# para Técnicas de Programación, FI UNAM.

## Abrir el proyecto

Extrae la carpeta completa. En Windows, abre `SimuRackon.sln` con Visual Studio 2022 (17.8 o posterior), la carga **Desarrollo de escritorio de .NET** y .NET 8. Selecciona **SimuRackon** como proyecto de inicio y presiona **F5**. También puedes ejecutar `dotnet run --project SimuRackon.Desktop` desde la carpeta de la solución.

## Editar la interfaz con Windows Forms

En el Explorador de soluciones, abre `SimuRackon.Desktop` (proyecto SimuRackon), haz clic derecho en `TorreForm.cs` y selecciona **Ver diseñador** (Mayús + F7). `TorreForm.Designer.cs` contiene los controles visuales y `TorreForm.cs` contiene los eventos y la lógica de presentación. Modifica los controles desde el diseñador y la ventana Propiedades. El mapa es un control personalizado (`MapaAeropuerto.cs`). Su contenedor estándar `contenedorMapa` se puede mover o redimensionar desde el diseñador. El mapa interactivo se crea al ejecutar el formulario y ocupa todo ese panel; en el diseñador el contenedor aparece vacío. Sus dibujos se editan en su propio código. No elimines ni cambies el nombre del contenedor sin actualizar el código correspondiente.

Solo existe un formulario. En el panel derecho, `formulario`, `panelResultados` y `panelConfirmacion` se alternan según el estado de la simulación. Para editarlos, usa **Ver > Otras ventanas > Esquema del documento** y selecciona el control correspondiente; también puedes desplazarte en el panel de autorización. Los paneles de resultados y confirmación son visibles en diseño y se ocultan al iniciar el programa.

## Atender el tráfico

Cada ronda comienza en 03:00 con 100 puntos y seis vuelos, tres llegadas y tres salidas. Puedes seleccionar cada vuelo desde el mapa o su tarjeta. Las columnas Salidas y Llegadas agrupan tres vuelos cada una. El mapa usa pistas horizontales, terminales alineadas y conexiones visibles para la pista seleccionada. Para las llegadas, cada botón reúne la distancia y el estado de la pista. Las pistas A, B y C están ocupadas por un despegue cada una al inicio.

Al autorizar un despegue, la pista se mantiene ocupada hasta terminar la operación: 0 s con sol, 1 s con nubes y 4 s con lluvia. En tormenta no se pueden autorizar aterrizajes ni despegues; una salida en curso se pausa y continúa al terminar la tormenta. El tiempo fijado al autorizar se conserva si el clima cambia sin tormenta. Los círculos a la derecha de las terminales muestran el avance por pista y los segundos restantes; una marca de verificación indica salida completada hasta que llegue el vuelo de reemplazo, y una cruz o un borde punteado indican bloqueo por tormenta. Las pistas se pueden atender de forma independiente. Al aterrizar, el vuelo ocupa una terminal conectada a la pista. A conecta con T1, B con T1 y T2, y C con T2 y T3.

El clima cambia automáticamente y al azar: sol de 45–65 s, nubes de 25–35 s, lluvia de 15–25 s y tormenta de 4–7 s. Solo puede haber tormenta después de lluvia. No hay botón de cambio manual. Son reglas educativas del simulador, no procedimientos aeronáuticos reales.

Cada operación completada suma 20 puntos y programa un vuelo nuevo en esa tarjeta diez segundos después de terminar. Los plazos corren de manera independiente. Al reaparecer una salida, entra en su pista. Al reaparecer una llegada, la terminal anterior queda libre tras el desembarque. Un intento en pista o terminal ocupada resta 10 puntos. La interfaz bloquea las autorizaciones durante la tormenta; el núcleo rechaza con -20 puntos una autorización prohibida que se intente directamente. Una ronda que termina antes de completar un despegue no suma esos puntos.

El marcador acumula todos los vuelos atendidos. A 00:00 se cierra el tráfico, se guarda el resultado en `sesiones.csv` junto al ejecutable y aparece el panel de puntuación dentro de la misma ventana. **Ver bitácora** regresa al panel de autorización con el resumen final. **Nueva ronda** empieza una ronda nueva.

## Generar el ejecutable para Windows

Para generar una copia lista para Windows x64, ejecuta `dotnet publish SimuRackon.Desktop -c Release -r win-x64 --self-contained false -o Aplicacion`. La computadora que abra `Aplicacion/SimuRackon.exe` necesita .NET Desktop Runtime 8 de 64 bits.

## Comprobar los tiempos de reposición

Ejecuta `dotnet run --project Pruebas` con el SDK .NET 8. Las pruebas usan un reloj controlado para verificar los 10 segundos, la coincidencia de vuelos, el vencimiento y el reinicio, además de los intervalos meteorológicos, las transiciones aleatorias, los tiempos de despegue, la pausa por tormenta y el cierre de ronda.


## Corrección de inicialización y prueba de interfaz

Esta revisión crea el mapa directamente en el constructor de `TorreForm`, después de `InitializeComponent`, y registra el evento de selección después de crearlo. Se usan campos explícitos `this.` en el diseñador. La suscripción a los eventos de tamaño e inicio se hace una vez inicializados los controles. En esta versión organizo las clases con regiones y comentarios, uso clases normales en lugar de records y cambio varias expresiones por ciclos y switch tradicionales. Mantengo el reloj, la lógica de operación y los eventos del formulario. Quito el pie de página institucional y acorto algunos textos de la pantalla.

Para evitar archivos compilados de la versión anterior, extrae el ZIP en una carpeta nueva, abre esa solución, y ejecuta **Compilar > Recompilar solución** antes de F5.

En Windows, abre una terminal en la carpeta de la solución y ejecuta `dotnet run --project PruebasInterfaz`. La prueba abre el formulario real y comprueba el mapa, la selección, los despegues, los aterrizajes, los cambios de clima, la reposición y el reinicio. No es necesario modificar el código de la aplicación para utilizarla. La prueba de interfaz espera los cambios automáticos del clima, por lo que tarda aproximadamente 45–75 segundos.
