% Chay a, b, c tren mot thu muc CSV. Sua dataDir/truthFile roi chay.
% Mac dinh tro vao DU LIEU GIA de thu; doi sang du lieu that khi co.
dataDir   = fullfile('..', 'tools', 'data', 'SYNTHETIC_DU_LIEU_GIA');
truthFile = fullfile(dataDir, 'truth_labels_SYNTHETIC.csv');
outDir    = fullfile('out');
a_survey(dataDir, outDir);
b_threshold(dataDir, outDir);
c_occupancy_eval(dataDir, truthFile, outDir);
