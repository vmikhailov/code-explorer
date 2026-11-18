<?php
namespace Mreg;

class Request
{
    public function sendCommand($commandName, $payload = array())
    {
        return array('status' => 200, 'command' => $commandName);
    }
}
