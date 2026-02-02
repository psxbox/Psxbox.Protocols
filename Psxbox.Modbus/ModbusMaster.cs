using Psxbox.Streams;
using Psxbox.Utils;

namespace Psxbox.Modbus;

public class ModbusMaster(IStream stream, IModbusWrapper modbusWrapper)
{

    public async Task<bool[]> ReadCoils(byte slaveId, ushort startAddress, ushort count)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildReadCoilsRequest(slaveId, startAddress, count);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.ReadCoilsFunc, responseSize, transactionId)
            .ConfigureAwait(false);
        byte[] bytes = ModbusHelpers.ParseReadCoilsResponse(response);
        var result = Converters.ByteArrayToBoolArray(span: bytes, reverse: false);

        return result[..count];
    }

    public async Task<bool[]> ReadDiscreteInputs(byte slaveId, ushort startAddress, ushort count)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildReadDiscreteInputsRequest(slaveId, startAddress, count);

        stream.Flush();

        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.ReadDiscreteInputsFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        var bytes = ModbusHelpers.ParseReadDiscreteInputsResponse(response);
        var result = Converters.ByteArrayToBoolArray(span: bytes, reverse: false);

        return result[..count];
    }

    public async Task<byte[]> ReadHoldingRegistersAsBytes(byte slaveId, ushort startAddress, ushort count)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildReadHoldingRegistersRequest(slaveId, startAddress, count);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.ReadHoldingRegistersFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        return ModbusHelpers.ParseReadHoldingRegistersResponse(response);
    }

    public async Task<ushort[]> ReadHoldingRegistersAsUshorts(byte slaveId, ushort startAddress, ushort count)
    {
        var result = await ReadHoldingRegistersAsBytes(slaveId, startAddress, count);
        return ModbusHelpers.BytesToUshorts(result);
    }

    public async Task<byte[]> ReadInputRegistersAsBytes(byte slaveId, ushort startAddress, ushort count)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildReadInputRegistersRequest(slaveId, startAddress, count);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.ReadInputRegistersFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        return ModbusHelpers.ParseReadInputRegistersResponse(response);
    }

    public async Task<ushort[]> ReadInputRegistersAsUshorts(byte slaveId, ushort startAddress, ushort count)
    {
        var result = await ReadInputRegistersAsBytes(slaveId, startAddress, count);
        return ModbusHelpers.BytesToUshorts(result);
    }

    public async Task WriteSingleCoil(byte slaveId, ushort address, bool value)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildWriteSingleCoilRequest(slaveId, address, value);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.WriteSingleCoilFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        // Check equality values
        ModbusHelpers.ParseWriteSingleCoilResponse(response, address, value);
    }

    public async Task WriteSingleRegister(byte slaveId, ushort address, ushort value)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildWriteSingleRegisterRequest(slaveId, address, value);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.WriteSingleRegisterFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        // Check equality values
        ModbusHelpers.ParseWriteSingleRegisterResponse(response, address, value);
    }

    public async Task WriteMultipleCoils(byte slaveId, ushort startAddress, ushort count, bool[] values)
    {
        var bytes = Converters.BoolArrayToByteArray(values);

        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildWriteMultipleCoilsRequest(slaveId, startAddress, count, bytes);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.WriteMultipleCoilsFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        // Check equality values
        ModbusHelpers.ParseWriteMultipleCoilsResponse(response, startAddress, count);
    }

    public async Task WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort count, ushort[] values)
    {
        (byte[] request, int responseSize, ushort transactionId) = modbusWrapper.BuildWriteMultipleRegistersRequest(slaveId, startAddress, count, values);
        stream.Flush();
        await stream.WriteAsync(request);

        byte[] response = await modbusWrapper
            .ReadResponse(stream, slaveId, ModbusHelpers.WriteMultipleRegistersFunc, responseSize, transactionId)
            .ConfigureAwait(false);

        // Check equality values
        ModbusHelpers.ParseWriteMultipleRegistersResponse(response, startAddress, count);
    }
}

