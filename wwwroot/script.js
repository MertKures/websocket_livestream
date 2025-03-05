let last_time_image_received = Date();

window.addEventListener('load', function () {
    var socket = new WebSocket('wss://<domain>/api/stream/ws?socket_type=1');

    // WebSocket event listeners
    socket.onopen = function () {
        console.log('WebSocket connection established.');
        // Perform any necessary actions after the connection is established
    };

    var img = document.getElementById('imageElement');

    socket.onmessage = function (event) {
        const current_time = Date();

        console.log(current_time - last_time_image_received, 'ms');

        last_time_image_received = current_time;

        // console.log('Received message:', event.data);
        var message = JSON.parse(event.data);
        if (message.type === 'IMAGE') {
            img.src = 'data:image/jpeg;base64,' + message.message;
        }
    };

    socket.onclose = function (event) {
        console.log('WebSocket connection closed with code:', event.code);
        // Perform any necessary actions after the connection is closed
    };

    socket.onerror = function (error) {
        console.error('WebSocket error:', error);
        // Handle any errors that occur during the WebSocket connection
    };
});