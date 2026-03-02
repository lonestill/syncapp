package com.ecosystem.android.model

data class Device(
    val name: String,
    val ipAddress: String,
    val port: Int,
    val platform: String, // "Windows" or "Android"
    var status: DeviceStatus = DeviceStatus.DISCONNECTED,
    var lastSeen: Long = System.currentTimeMillis()
) {
    fun getEndpoint(): String = "$ipAddress:$port"
    
    override fun toString(): String = "$name ($platform) - ${getEndpoint()} [$status]"
}

enum class DeviceStatus {
    DISCONNECTED,
    DISCOVERED,
    CONNECTED,
    CONNECTING,
    ERROR
}