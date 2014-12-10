// Include the cluster module
var cluster = require('cluster');

// Code to run if we're in the master process
if (cluster.isMaster) {
    console.log('Master process');
    // Count the machine's CPUs
    var cpuCount = require('os').cpus().length;

    // Create a worker for each CPU
    for (var i = 0; i < cpuCount; i += 1) {
        cluster.fork();
    }

    // Listen for dying workers
    cluster.on('exit', function (worker) {

        // Replace the dead worker, we're not sentimental
        console.log('Worker ' + worker.id + ' died :(');
        cluster.fork();

    });

// Code to run if we're in a worker process
} else {

    /*// Include Express
    var express = require('express');

    // Create a new Express application
    var app = express();

    // Add a basic route – index page
    app.get('/', function (req, res) {
        //var i = 1000000000;
        //while (--i);
        res.send('Hello from Worker ' + cluster.worker.id);
    });

    // Bind to a port
    app.listen(3000);*/

    var ftpd = require('ftpd');
    var fs = require('fs');

    var options = {
      pasvPortRangeStart: 4000,
      pasvPortRangeEnd: 5000,
      getInitialCwd: function(connection, callback) {
        var userPath = '/Users/Julien/Downloads';//process.cwd() + '/' + connection.username;
        fs.exists(userPath, function(exists) {
          exists ? callback(null, userPath) : callback('path does not exist', userPath);
        });
      },
      getRoot: function(user) {
        return '/';
      }
    };

    var host = '127.0.0.1';

    var server = new ftpd.FtpServer(host, options);

    server.on('client:connected', function(conn) {
      var username;
      console.log('Client connected from ' + conn.socket.remoteAddress);
      conn.on('command:user', function(user, success, failure) {
        username = user;
        (user == 'john') ? success() : failure();
      });
      conn.on('command:pass', function(pass, success, failure) {
        // check the password
        (pass == 'bar') ? success(username) : failure();
      });
    });

    server.listen(21);
    console.log('FTPD listening on port 21');


    console.log('Worker ' + cluster.worker.id + ' running!');

}