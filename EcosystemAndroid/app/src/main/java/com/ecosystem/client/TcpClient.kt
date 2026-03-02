package com.ecosystem.client

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.DataInputStream
import java.io.DataOutputStream
import java.net.Socket
import java.nio.charset.StandardCharsets

object TcpClient {
    suspend fun sendTcpMessage(ip: String, port: Int, message: String): String = withContext(Dispatchers.IO) {
        return@withContext try {
            Socket(ip, port).use { socket ->
                val dataOut = DataOutputStream(socket.getOutputStream())
                val dataIn = DataInputStream(socket.getInputStream())

                val payloadBytes = message.toByteArray(StandardCharsets.UTF_8)

                // 1. Write the 4-byte Int32 length prefix (Big-Endian)
                dataOut.writeInt(payloadBytes.size)

                // 2. Write the UTF-8 payload bytes
                dataOut.write(payloadBytes)
                dataOut.flush()

                // 3. Wait for the ACK response from the C# server
                val responseLength = dataIn.readInt()
                
                val responseBytes = ByteArray(responseLength)
                dataIn.readFully(responseBytes)

                // 4. Decode the response
                String(responseBytes, StandardCharsets.UTF_8)
            }
        } catch (e: Exception) {
            "Failure: ${e.message}"
        }
    }
}
