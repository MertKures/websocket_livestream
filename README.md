# websocket_livestream
This project uses Asp Net Core and Websockets to create a simple website and API that allows a user to send live stream to the website for others to watch. At the moment, it does not support any kind of stream codecs and simply just sends frames as base64. 480p, colored, %50 quality jpeg images are used to test this application. It get ~10 FPS when each image has 5-6 KB.

**Future plans**
1) Using H264 or H265 codecs to send only changed frames to optimize bandwidth and frames per second.
