# Inventario con caché (en algún momento)
En este ejercicio, nos encontramos con una API que conecta a un servicio antiguo de control de inventario.
Este servicio no está pensado para integrar con facilidad, y tiene unos problemas de rendimiento importantes que no se pueden evitar directamente.

Actualmente presenta un rendimiento muy pobre, y tiene serios problemas para manejar solicitudes concurrentes, si se ejecuta el proyecto de test, se puede comprobar que incluso con dos solicitudes en paralelo, el stock no se retira correctamente, dando resultados inválidos.

## Limitaciones
- El servicio WarehouseService no puede ser modificado, es el servicio que simula la lentitud del servicio con el que estamos integrando.
- El proyecto de test no se puede modificar.
- Tocar los ficheros `json` tampoco está permitido, son los ficheros que simulan el stock de la base de datos servicio de inventario.
- Se puede modificar la implementación de los puntos de entrada de la API, pero no la ruta ni los tipos de los valores que retornan.

## Objetivos
### Rendimiento
Introducir algún tipo de almacenamiento intermedio que permita mejorar el rendimiento de la aplicación. El tiempo de respuesta promedio debería caer dramáticamente con un número mayor de respuestas concurrentes.
### Concurrencia
Intentar retirar stock del mismo producto en paralelo y de forma concurrente debería funcionar correctamente. Actualmente, si se retiran cinco productos en paralelo, el stock se reduce solamente acorde con una de las solicitudes.
### Consistencia
El sistema antiguo se sigue utilizando, como mucho, **diez segundos y medio** después de que se haya actualizado el stock en la API, el sistema antiguo debería reflejar el cambio (el proyecto de test se encarga de comprobar esto).

## Recursos
Absolutamente todo lo que deseen, ChatGPT, llamar a un primo que sabe de esto, trabajar en grupo o individualmente, preguntarme a mí... ¡Lo que sea! Solamente recuerda que lo valioso del ejercicio es el aprendizaje, y que tendremos una charla para que me expliques qué has hecho y por qué.

## Entrega
Parcial o completa, poco a poco, no se espera que se resuelva en ejercicio completo, pero sí que se muestre un progreso en la comprensión del problema y en el planteamiento de una solución.

## Pistas
- El primer objetivo es el más sencillo con diferencia, el tercero podría ser el segundo más simple, pero resolverlo primero complicará el segundo casi con total seguridad, no recomiendo seguir ese orden, pero es una opción.
- El proyecto incluye la palabra cache en el nombre, pero una caché no tiene por qué ser la solución, ni algo tiene por qué llamarse caché para serlo (ficheros, una base de datos SQL...).
  - Ten en cuenta que la tecnología que uses para la caché será, con total seguridad, una solución que facilitará partes del problema y complicará otras.
    - Una caché en memoria mejorará el rendimiento, pero complicará la consistencia y la concurrencia.
    - Una base de datos SQL mejorará la consistencia, y la concurrencia, pero conlleva más esfuerzo de configuración y uso, y es más lenta que una caché en memoria (partimos de un sistema que tarda 2.5 segundos por solicitud, así que no sufras, cualquier mejora será espectacular en comparación).
    - Una base de datos NoSQL podría ser una solución intermedia, pero no es tan sencillo como una caché en memoria ni tan consistente como una base de datos SQL.
- No te preocupes por la seguridad, no es un problema en este ejercicio.
- No te preocupes por la escalabilidad, no es un problema en este ejercicio.
- Puedes asumir que nunca va a haber dos copias de esta API ejecutándose en paralelo, no pienses más allá de un único servicio en ejecución.

## Notas
La concurrencia y la consistencia son problemas difíciles, no te preocupes si no consigues resolverlos, lo importante es que lo intentes y que entiendas lo que vas haciendo a cada paso.
