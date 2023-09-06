// Set the date we're counting down to
var countDownDate = new Date();

const timeDisplay = document.querySelector('.display__time-left');
let then = Date.now();

let countdown = setInterval(() => {
    const secondsLeft = Math.round((then - Date.now()) / 1000);
    //display it
    if (secondsLeft <= 0) {
        then = Date.now();
        timeDisplay.textContent = "00:00:00";
        return;
    }
    displayTimeLeft(secondsLeft);
}, 1000)

function displayTimeLeft(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = (Math.floor(seconds / 60)) % 60;
    const reminderSeconds = seconds % 60;
    const display = `${hours < 10 ? '0' : ''}${hours}:${minutes < 10 ? '0' : ''}${minutes}:${reminderSeconds < 10 ? '0' : ''}${reminderSeconds}`;
    timeDisplay.textContent = display;
}

function IncreaseTime(number) {
    then += number * 1000 * 60
}

function Reset() {
    then = Date.now();
    timeDisplay.textContent = "00:00:00";
}