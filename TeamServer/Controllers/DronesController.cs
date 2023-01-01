﻿using AutoMapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using SharpC2.API;
using SharpC2.API.Responses;

using TeamServer.Drones;
using TeamServer.Hubs;
using TeamServer.Interfaces;

namespace TeamServer.Controllers;

[Authorize]
[ApiController]
[Route(Routes.V1.Drones)]
public class DronesController : ControllerBase
{
    private readonly IDroneService _drones;
    private readonly IPeerToPeerService _peerToPeer;
    private readonly IMapper _mapper;
    private readonly IHubContext<NotificationHub, INotificationHub> _hub;
    private readonly IReversePortForwardService _portForwards;

    public DronesController(IDroneService drones, IMapper mapper, IHubContext<NotificationHub, INotificationHub> hub, IReversePortForwardService portForwards, IPeerToPeerService peerToPeer)
    {
        _drones = drones;
        _mapper = mapper;
        _hub = hub;
        _portForwards = portForwards;
        _peerToPeer = peerToPeer;
    }

    [HttpGet]
    public async Task<ActionResult<DroneResponse>> GetDrones()
    {
        var drones = await _drones.Get();
        var response = _mapper.Map<IEnumerable<Drone>, IEnumerable<DroneResponse>>(drones);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DroneResponse>> GetDrone(string id)
    {
        var drone = await _drones.Get(id);

        if (drone is null)
            return NotFound();

        var response = _mapper.Map<Drone, DroneResponse>(drone);
        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDrone(string id)
    {
        var drone = await _drones.Get(id);

        if (drone is null)
            return NotFound();

        // remove vertex
        _peerToPeer.RemoveVertex(drone.Metadata.Id);
        
        await _drones.Delete(drone);
        await _hub.Clients.All.DroneDeleted(drone.Metadata.Id);
        
        // also delete any pivots
        var forwards = (await _portForwards.GetAll(drone.Metadata.Id)).ToArray();

        if (forwards.Any())
            await _portForwards.Delete(forwards);

        return NoContent();
    }
}